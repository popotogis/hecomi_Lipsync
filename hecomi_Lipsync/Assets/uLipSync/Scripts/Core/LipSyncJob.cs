using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace uLipSync
{

[BurstCompile]
public struct LipSyncJob : IJob
{
    public struct Result
    {
        public int index; //求めたMFCCがどの音素に近かったかを格納する(?)
        public float volume;
        public float distance; //
    }

    [ReadOnly] public NativeArray<float> input;
    [ReadOnly] public int startIndex;
    [ReadOnly] public int outputSampleRate; 
    [ReadOnly] public int targetSampleRate; //16k
    [ReadOnly] public int melFilterBankChannels; //24
    [ReadOnly] public float volumeThresh;
    public NativeArray<float> mfcc;
    public NativeArray<float> phonemes;
    public NativeArray<Result> result;

    public void Execute()
    {
        float volume = Algorithm.GetRMSVolume(input);
        if (volume < volumeThresh)
        {
            var res1 = result[0];
            res1.index = -1;
            res1.volume = volume;
            res1.distance = float.MaxValue;
            result[0] = res1;
            return;
        }

        // Copy input ring buffer to a temporary array
        NativeArray<float> buffer;
        Algorithm.CopyRingBuffer(input, out buffer, startIndex);

        // LowPassFilter
        int cutoff = targetSampleRate / 2;
        int range = targetSampleRate / 4;
        Algorithm.LowPassFilter(ref buffer, outputSampleRate, cutoff, range);

        // Down sample
        NativeArray<float> data;
        Algorithm.DownSample(buffer, out data, outputSampleRate, targetSampleRate);

        // Pre-emphasis
        Algorithm.PreEmphasis(ref data, 0.97f);

        // Multiply window function
        Algorithm.HammingWindow(ref data); //ハミング窓を使っている

        // FFT
        NativeArray<float> spectrum;
        Algorithm.FFT(data, out spectrum);

        // Mel-Filter Bank
        NativeArray<float> melSpectrum;
        Algorithm.MelFilterBank(spectrum, out melSpectrum, targetSampleRate, melFilterBankChannels);

        // Log
        for (int i = 0; i < melSpectrum.Length; ++i)
        {
            melSpectrum[i] = math.log10(melSpectrum[i]);
        }

        // DCT 離散コサイン変換
        NativeArray<float> melCepstrum;
        Algorithm.DCT(melSpectrum, out melCepstrum);

        // MFCC
        for (int i = 1; i < 13; ++i)
        {
            mfcc[i - 1] = melCepstrum[i];
        }

        // Result
        // index, volume, distance(音素, 音量, 距離)をresult[0]にして返す
        var res = new Result();
        res.volume = volume;
        GetVowel(ref res.index, ref res.distance);
        result[0] = res;

        melCepstrum.Dispose();
        melSpectrum.Dispose();
        spectrum.Dispose();
        data.Dispose();
        buffer.Dispose();
    }

    void GetVowel(ref int index, ref float minDistance) //refで渡すとメソッド内で行われた変更が参照元にも反映される。returnしなくていい。
    {
        minDistance = float.MaxValue; //最初はMaxValue(最大の値)を入れておく
        int n = phonemes.Length / 12; //音素の数（ex.A,I,U,E,O）×12（分析で使われる係数の数）から元の音素の数を求めている。音素の数だけ以下のfor文のが回る
        for (int i = 0; i < n; ++i)
        {
            var distance = CalcTotalDistance(i);
            if (distance < minDistance)
            {
                index = i;
                minDistance = distance;
            }
        }
    }

    float CalcTotalDistance(int index) //
    {
        var distance = 0f;
        int offset = index * 12;
        for (int i = 0; i < mfcc.Length; ++i)
        {
            distance += math.abs(mfcc[i] - phonemes[i + offset]);
        }
        return distance;
    }
}

}
