using UnityEngine;

/// <summary>
/// 让一个物体绕自己的 Z 轴匀速旋转 —— 给「会动的小景」用（风车叶片、以后的水车）。
/// 转的是它自己的 <c>localRotation</c>，所以挂在谁下面都行；
/// 物体随区块回收时一起销毁，不需要额外收拾。
/// </summary>
public class Spinner : MonoBehaviour
{
    [Tooltip("每秒转多少度（负数就是反着转）")]
    public float degreesPerSecond = 45f;

    [Tooltip("开局随机一个相位，免得整片地区的风车像军训一样同步")]
    public bool randomPhase = true;

    void Start()
    {
        if (randomPhase) transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    void Update()
    {
        if (degreesPerSecond == 0f) return;
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
