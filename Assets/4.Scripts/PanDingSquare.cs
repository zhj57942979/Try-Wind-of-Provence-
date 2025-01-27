using UnityEngine;
using UnityEngine.Video;

public class PanDingSquare : MonoBehaviour
{
    /// <summary>
    /// 对外缓存的组件
    /// </summary>
    [HideInInspector] public GameObject go;


    /// <summary>
    /// 移动速度（向右水平），每个音符各有一个。以 帧 作为时间单位
    /// </summary>
    private Vector2[] velocity = new Vector2[1];

    /// <summary>
    /// 判定块最初的位置
    /// </summary>
    private Vector2 initialPosition;

    /// <summary>
    /// 判定块终止移动的位置
    /// </summary>
    private Vector2 endPosition;

    /// <summary>
    /// 要触碰的这一组（masako：亚组）内第几个音符了 （从0开始）
    /// </summary>
    private int whichNote;

    /// <summary>
    /// 要触碰的这一组（masako：亚组）内有几个音符
    /// </summary>
    private int noteCount;

    /// <summary>
    /// 缓存触碰的音符碰撞箱（trigger）的变换组件
    /// </summary>
    private Transform noteTriggerTransform;

    private Rigidbody2D rd;


#if UNITY_EDITOR
    private float videoSpeed = 1;
#endif

    private void Awake()
    {
        go = gameObject;
        rd = GetComponent<Rigidbody2D>();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 仅用于
    /// </summary>
    /// <param name="videoPlayer"></param>
    public void OnlyForEditor(VideoPlayer videoPlayer)
    {
        videoSpeed = videoPlayer.playbackSpeed;
    }
#endif


    /// <summary>
    /// 设定回车和结束
    /// </summary>
    /// <param name="initialPosition">最初的位置</param>
    public void SetEnterAndEnd(Vector2 whereinitialPosition, Vector2 whererEndPosition)
    {
        initialPosition = whereinitialPosition;
        endPosition = whererEndPosition;
    }

    /// <summary>
    /// 设定速度（性能消耗大：Editor模式下有个Debug）
    /// </summary>
    /// <param name="targetFrame">目标音符的视频帧率（计算时间）</param>
    /// <param name="myFrame">判定方块自己的视频帧率（计算时间）</param>
    /// <param name="targetLocation">判定方块自己的位置（计算距离）</param>
    /// <param name="myLocation">判定方块自己的位置（计算距离）</param>
    public void SetVelocity(int[] targetFrame, int[] myFrame, Vector2[] targetLocation, Vector2[] myLocation)
    {
        //先回车，然后计算，准备移动
        Enter();

#if UNITY_EDITOR
        if (targetFrame.Length != myFrame.Length || targetFrame.Length != targetLocation.Length ||
            myFrame.Length != targetLocation.Length)
        {
            Debug.LogError("这里要求三个数组的长度相同");
        }
#endif
        //得到并记录这一组（亚组）内音符的数量
        noteCount = targetFrame.Length;
        velocity = new Vector2[noteCount + 1]; //多给个，防止数组炸了，md摆烂了

        for (int i = 0; i < myFrame.Length; i++)
        {
            float a = targetLocation[i].x - myLocation[i].x;
            float b = targetFrame[i] - myFrame[i];
            velocity[i] = new Vector2(a / b, 0f);

#if UNITY_EDITOR
            //    Debug.LogFormat("targetFrame:{0} myFrame:{1} targetLocation:{2} myLocation:{3} velocity:{4} {5}/{6}",
            //        targetFrame[i], myFrame[i], targetLocation[i], myLocation[i], velocity[i], a, b);
#endif
        }

        ////多给个，防止数组炸了，md摆烂了
        velocity[^1] = velocity[^2];
    }

    /// <summary>
    /// 按照每秒30次执行
    /// </summary>
    public void FixedUpdate()
    {
#if UNITY_EDITOR
        rd.MovePosition(videoSpeed * 2 * velocity[whichNote] + rd.position);
#else
       rd.MovePosition( 2 * velocity[whichNote] + rd.position);
#endif
        //到头之后，回到起始位置
        //whichNote != 0：防止到达终止位置之后，多次调用本方法
        if (rd.position.x >= endPosition.x && whichNote != 0)
        {
            Enter();
        }
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        //当判定方块触碰到最后这一组（亚组）最后一个音符时，不在缓存变换组件。
        //因为最后一个音符不在按照中线进行判断，而是用TriggerExit2D
        if (whichNote < noteCount) noteTriggerTransform = other.transform;
    }

    /// <summary>
    /// 判定方块（自己）与音符的层都是Note，只有他俩之间能碰撞。这个就是碰撞判定用的
    /// </summary>
    /// <param name="other"></param>
    public void OnTriggerStay2D(Collider2D other)
    {
        //判定滑块到音符了，该换成下一个速度了
        if (noteTriggerTransform == null) return;
        //按照中线判断，向下一个音符移动，并运用对应的速度
        //whichNote < noteCount 防止多次调用
        if (!(rd.position.x >= noteTriggerTransform.position.x) || whichNote >= noteCount) return;
        //记录个数
        whichNote++;
        //清除掉这个正在触碰的音符的变换组件，防止多次调用
        noteTriggerTransform = null;
    }


    /// <summary>
    /// 回车
    /// </summary>
    public void Enter()
    {
        rd.position = initialPosition;
        whichNote = 0;
        velocity[whichNote] = Vector2.zero;
    }
}