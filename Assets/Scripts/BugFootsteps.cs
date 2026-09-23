using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 小虫的脚步：按走过的距离触发 <see cref="AudioKeys.Step"/>，走得越快步子越密。
/// 组件由自己在场景加载后挂到小虫身上，不需要在场景里手工添加；
/// 没提供 <c>step</c> 音频时什么都不发生。
/// </summary>
public class BugFootsteps : MonoBehaviour
{
    [Tooltip("走多少世界单位响一次脚步")]
    public float strideLength = 0.6f;
    [Tooltip("两次脚步之间的最短间隔，冲刺时不会连成一片")]
    public float minInterval = 0.09f;
    [Tooltip("比这个还慢（或躲在地洞里）就不出声")]
    public float minSpeed = 0.2f;
    [Tooltip("脚步音量")]
    public float volume = 0.7f;
    [Tooltip("音高随机浮动，避免听起来像复读（0 = 每次一样）")]
    public float pitchVariation = 0.08f;

    BugController bug;
    float distance;
    float nextStepTime;

    /// <summary>场景加载后把小虫找出来并挂上脚步组件（已经在上面就跳过）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Attach();
        SceneManager.sceneLoaded += (scene, mode) => Attach();
    }

    static void Attach()
    {
        BugController target = FindObjectOfType<BugController>();
        if (target == null) return;
        if (target.GetComponent<BugFootsteps>() != null) return;
        target.gameObject.AddComponent<BugFootsteps>();
    }

    void Awake()
    {
        bug = GetComponent<BugController>();
    }

    void Update()
    {
        if (bug == null)
        {
            enabled = false;
            return;
        }

        float speed = bug.CurrentSpeed;
        if (bug.IsHidden || speed < minSpeed) return;   // 躲在地洞里 / 站着不动：不出声

        distance += speed * Time.deltaTime;
        if (distance < strideLength || Time.time < nextStepTime) return;

        distance = 0f;
        nextStepTime = Time.time + minInterval;
        AudioOverridePlayer.Play(AudioKeys.Step, volume, 1f + Random.Range(-pitchVariation, pitchVariation));
    }
}
