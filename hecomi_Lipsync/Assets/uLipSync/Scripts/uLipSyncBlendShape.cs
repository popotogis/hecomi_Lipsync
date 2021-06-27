using UnityEngine;
using System.Collections.Generic;

namespace uLipSync
{

public class uLipSyncBlendShape : MonoBehaviour
{
    [System.Serializable]
    public class BlendShapeInfo
    {
        public string phoneme;
        public int index = -1;
        public float maxWeight = 1f;
        public float vowelChangeVelocity { get; set; } = 0f;
        public float weight { get; set; } = 0f;
        public float normalizedWeight { get; set; } = 0f;
    }

    public SkinnedMeshRenderer skinnedMeshRenderer;
    public List<BlendShapeInfo> blendShapes = new List<BlendShapeInfo>();
    public bool applyVolume = false;
    [Range(0f, 0.2f)] public float openDuration = 0.05f;
    [Range(0f, 0.2f)] public float closeDuration = 0.1f;
    [Range(0f, 0.2f)] public float vowelChangeDuration = 0.04f;

    float openVelocity_ = 0f;
    float closeVelocity_ = 0f;
    List<float> vowelChangeVelocity_ = new List<float>();

    string phoneme = "";
    float volume = 0f;
    bool lipSyncUpdated = false;

    public void OnLipSyncUpdate(LipSyncInfo lipSync) //uLipSync.csで求めた発話中の音素やその音量などのデータを使って口の形をかえる
    {
        phoneme = lipSync.phoneme;
        //Debug.Log(phoneme); //あいうえおのうちどれを発音しているかをAIUEOで表示

        if (lipSync.volume > Mathf.Epsilon) //ギリギリ0でない最小の値 limみたいなイメージ。0じゃダメな理由はなんだろう？
        {
            var targetVolume = applyVolume ? lipSync.volume : 1f; //条件演算子 applyVolumeはデフォルトではfalse
            volume = Mathf.SmoothDamp(volume, targetVolume, ref openVelocity_, openDuration); //Mathf.Smoothdamp(current,target,currentVelocity,smoothTime)
                                                                                              //current:現在位置, target:目標地, currentVelocity:現在の速度, smoothTime:targetに到達するまでの時間
        }
        else
        {
            var targetVolume = applyVolume ? lipSync.volume : 0f;
            volume = Mathf.SmoothDamp(volume, targetVolume, ref closeVelocity_, closeDuration);
        }

        lipSyncUpdated = true;
    }

    void Update()
    {
        float sum = 0f;

        foreach (var bs in blendShapes)
        {
            float targetWeight = (bs.phoneme == phoneme) ? 1f : 0f;
            float vowelChangeVelocity = bs.vowelChangeVelocity;
            bs.weight = Mathf.SmoothDamp(bs.weight, targetWeight, ref vowelChangeVelocity, vowelChangeDuration);
            bs.vowelChangeVelocity = vowelChangeVelocity;
            sum += bs.weight;
        }

        foreach (var bs in blendShapes)
        {
            bs.normalizedWeight = sum > 0f ? bs.weight / sum : 0f;
        }
    }

    void LateUpdate()
    {
        if (!skinnedMeshRenderer) return;

        foreach (var bs in blendShapes)
        {
            if (bs.index < 0) continue;
                //Debug.Log("bs.index" + "=" + bs.index); //bs.indexの値を表示
                //Debug.Log(skinnedMeshRenderer.GetBlendShapeWeight(bs.index)); //各BlendShapeのWeightを表示
                skinnedMeshRenderer.SetBlendShapeWeight(bs.index, 0f);
        }

        foreach (var bs in blendShapes)
        {
            if (bs.index < 0) continue;

            float weight = skinnedMeshRenderer.GetBlendShapeWeight(bs.index);
            weight += bs.normalizedWeight * bs.maxWeight * volume * 100;
            skinnedMeshRenderer.SetBlendShapeWeight(bs.index, weight);
        }

        if (lipSyncUpdated)
        {
            lipSyncUpdated = false;
        }
        else
        {
            volume = Mathf.SmoothDamp(volume, 0f, ref closeVelocity_, closeDuration);
        }
    }

#if UNITY_EDITOR
    public void AddBlendShapeInfo()
    {
        var info = new BlendShapeInfo();
        info.phoneme = "Phoneme";
        blendShapes.Add(info);
    }

    public void RemoveBlendShape(int index)
    {
        if (index < 0 || index >= blendShapes.Count) return;
        blendShapes.RemoveAt(index);
    }
#endif
}

}

