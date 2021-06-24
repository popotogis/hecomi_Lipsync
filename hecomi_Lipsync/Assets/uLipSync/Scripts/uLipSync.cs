using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace uLipSync
{

public class uLipSync : MonoBehaviour
{
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
    public Profile profile; //プロファイル(Male, Female, Create)
    public LipSyncUpdateEvent onLipSyncUpdate = new LipSyncUpdateEvent();
    [Tooltip("If you want to supress the sound output, set this value to zero instead of setting the AudioSource volume to zero")] //ParametersのOutput Sound Gainの部分
    [Range(0f, 1f)] public float outputSoundGain = 1f;

    JobHandle jobHandle_;
    object lockObject_ = new object();
    int index_ = 0;

    NativeArray<float> rawInputData_; //NativeArray: C#がメモリ空間としてマネージドヒープを利用するのと違い、ネイティブメモリを使用する配列。使い終わったらDisposeで開放しないとメモリリークを起こす。
    NativeArray<float> inputData_;
    NativeArray<float> mfcc_;
    NativeArray<float> mfccForOther_;
    NativeArray<float> phonemes_; //音素の数（あいうえおの5つ）
    NativeArray<LipSyncJob.Result> jobResult_;
    List<int> requestedCalibrationVowels_ = new List<int>();

    public NativeArray<float> mfcc { get { return mfccForOther_; } }
    public LipSyncInfo result { get; private set; } = new LipSyncInfo();

    int inputSampleCount //音声のサンプル数(uLipSync.inputSampleCountとすることで他のスクリプトでも参照可能)
    {
        get 
        {  
            float r = (float)AudioSettings.outputSampleRate / profile.targetSampleRate; //AudioSettings.outputSmapleRate ミキサーの現在の出力レートを返す
            return Mathf.CeilToInt(profile.sampleCount * r); //Mathf.CeilToInt(float f) f以上の最小の整数を返す
        }
    }

    void OnEnable() //オブジェクトが有効になった時に呼び出される
    {
        AllocateBuffers();
    }

    void OnDisable() //オブジェクトが向こうになった時に呼び出される
    {
        DisposeBuffers();
    }

    void Update()
    {
        if (!jobHandle_.IsCompleted)//Jobが終わっている(true)なら下の処理を行う.終わっていない(false)ならreturn
        {
                //Debug.Log("RETURN"); //Jobが終わっていないのでreturn
                return;
        }
        //Debug.Log("NOT RETURN"); //Jobが終わっているので以下の処理を行う

        sw.Stop(); //計測終了
        Debug.Log("LipSyncJob End" + sw.Elapsed.Milliseconds + "ms");
        UpdateResult();
        InvokeCallback();
        UpdateCalibration();
        UpdatePhonemes();
        ScheduleJob();

        UpdateBuffers();
    }

    void AllocateBuffers() //バッファ割り当て
        {
        lock (lockObject_) //Jobを呼び出すために入出力用NativeArrayを確保
        {
            int n = inputSampleCount;
            rawInputData_ = new NativeArray<float>(n, Allocator.Persistent); //Allocator.Persistent インスタンスの寿命が無制限（手動で解放されるまで）
            inputData_ = new NativeArray<float>(n, Allocator.Persistent); 
            mfcc_ = new NativeArray<float>(12, Allocator.Persistent); 
            jobResult_ = new NativeArray<LipSyncJob.Result>(1, Allocator.Persistent); //出力
            mfccForOther_ = new NativeArray<float>(12, Allocator.Persistent); 
            phonemes_ = new NativeArray<float>(12 * profile.mfccs.Count, Allocator.Persistent);
        }
    }

    void DisposeBuffers() //バッファ廃棄
        {
        lock (lockObject_)
        {
            jobHandle_.Complete();
            rawInputData_.Dispose();
            inputData_.Dispose();
            mfcc_.Dispose();
            mfccForOther_.Dispose();
            jobResult_.Dispose();
            phonemes_.Dispose();
        }
    }

    void UpdateBuffers() //バッファをリセット
    {
        if (inputSampleCount != rawInputData_.Length ||
            profile.mfccs.Count * 12 != phonemes_.Length)
        {
            lock (lockObject_)
            {
                DisposeBuffers();
                AllocateBuffers();
            }
        }
    }

    void UpdateResult()
    {
        jobHandle_.Complete();
        mfccForOther_.CopyFrom(mfcc_);

        var index = jobResult_[0].index;
        var phoneme = profile.GetPhoneme(index);
        float distance = jobResult_[0].distance;
        float vol = Mathf.Log10(jobResult_[0].volume);
        float minVol = profile.minVolume;
        float maxVol = Mathf.Max(profile.maxVolume, minVol + 1e-4f);
        vol = (vol - minVol) / (maxVol - minVol);
        vol = Mathf.Clamp(vol, 0f, 1f); //volを0-1の間に制限

            result = new LipSyncInfo()
        {
            index = index,
            phoneme = phoneme,
            volume = vol,
            rawVolume = jobResult_[0].volume,
            distance = distance,
        };
    }

    void InvokeCallback()
    {
        if (onLipSyncUpdate == null) return;

        onLipSyncUpdate.Invoke(result);
    }

    void UpdatePhonemes()
    {
        int index = 0;
        foreach (var data in profile.mfccs)
        {
            foreach (var value in data.mfccNativeArray)
            {
                if (index >= phonemes_.Length) break;
                phonemes_[index++] = value;
            }
        }
    }

    void ScheduleJob()
    {
        int index = 0;
        lock (lockObject_)
        {
            inputData_.CopyFrom(rawInputData_);
            index = index_;
        }

        var lipSyncJob = new LipSyncJob()
        {
            input = inputData_,
            startIndex = index,
            outputSampleRate = AudioSettings.outputSampleRate,
            targetSampleRate = profile.targetSampleRate,
            volumeThresh = Mathf.Pow(10f, profile.minVolume),
            melFilterBankChannels = profile.melFilterBankChannels,
            mfcc = mfcc_,
            phonemes = phonemes_,
            result = jobResult_,
        };

        jobHandle_ = lipSyncJob.Schedule(); //.Schedule() Jobを実行する時に使う
            Debug.Log("LipSyncJob Start"); //計測開始
            sw.Restart();

        }

    void OnAudioFilterRead(float[] input, int channels)
    {
        if (rawInputData_ == null) return;

        lock (lockObject_)
        {
            index_ = index_ % rawInputData_.Length;
            for (int i = 0; i < input.Length; i += channels) 
            {
                rawInputData_[index_++ % rawInputData_.Length] = input[i];
            }
        }

        if (math.abs(outputSoundGain - 1f) > math.EPSILON)
        {
            for (int i = 0; i < input.Length; ++i) 
            {
                input[i] *= outputSoundGain;
            }
        }
    }

    public void RequestCalibration(int index)
    {
        requestedCalibrationVowels_.Add(index);
    }

    void UpdateCalibration()
    {
        if (profile == null) return;

        foreach (var index in requestedCalibrationVowels_)
        {
            profile.UpdateMfcc(index, mfcc, true);
        }

        requestedCalibrationVowels_.Clear();
    }
}

}
