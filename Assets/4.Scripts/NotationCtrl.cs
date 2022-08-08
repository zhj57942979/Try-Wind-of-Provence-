using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Video;
using YamlDotNet.Serialization;

public class NotationCtrl : MonoBehaviour
{
    
    
#if UNITY_EDITOR

    #region EDBUG用。用完了记得删了

    [Header("UI与屏幕内容")]
    public Transform go;
    public TMP_Text frameCount;

    #endregion

#endif

    /// <summary>
    /// 储存两位路人王时间的结构体
    /// </summary>
    [System.Serializable]
    public struct Timeline
    {
        public string[] Time;
    }

    public VideoPlayer videoPlayer;
    public Camera cameraUI;

    public RectTransform[] panDingsInitialLocation;
    
    
   /// <summary>
    /// 两位的判定方块
    /// </summary>
    public PanDingSquare[] panDingSquares;
    
    //友好的显示音符出现的时间
    public Timeline Junna;
    public Timeline Masako;

    
    /// <summary>
    /// 铺面滞后数。越大铺面开始的时间越晚。负数则提前出现
    /// </summary>
    [Header("设置")]
    public int lag = 0;
   

    /// <summary>
    /// Junna的音符显示的位置
    /// </summary>
    [Header("UI与屏幕内容")]
    public RectTransform[] JunnaNoteLocation;
    public RectTransform[] MasakoNoteLocation;


//音符预设
    public Notes quarterNoteWithLine;
    public Notes eighthNoteWithLine;
    public Notes eighthNoteWithPoint;
    public Notes eighthNote;

    public CanvasGroup percussionCanvas;
    [Header("节拍器")] public Metronome metronome;
    public CanvasGroup metronomeCanvas;
    
#if UNITY_EDITOR
    /// <summary>
    /// 要用哪一个时间点了 Junna Masaka
    /// </summary>
    private int[] timePointToUse = { 0, 0 };
#endif
   
    
    /// <summary>
    /// (Junna)生成的音符池
    /// </summary>
    private List<Notes> notesPoolForJunna = new List<Notes>();

    /// <summary>
    /// (Masako)生成的音符池
    /// </summary>
    private List<Notes> notesPoolForMasako = new List<Notes>();

    /// <summary>
    /// Junna用的间隔记录（即每一组的结尾是第几个音符）
    /// </summary>
    private List<int> JunnaInterval = new List<int>();

    /// <summary>
    /// Masako用的间隔记录（即每一组的结尾是第几个音符）
    /// </summary>
    private List<int> MasakoInterval = new List<int>();

    /// <summary>
    /// 接下来该用Junna的哪一组间隔符号了
    /// </summary>
    private int whichIntervalToUseForJunna = 0;

    /// <summary>
    /// 接下来该用Masako的哪一组间隔符号了
    /// </summary>
    private int whichIntervalToUseForMasako = 0;

    /// <summary>
    /// Masako间隔分组内，每8个为一个亚组，该用那个亚组了
    /// </summary>
  //  private int subGroupToUse = 0;

    //将友好型Timeline，转化为的以0.1s为单位的协程可用的时间
    private List<int> JunnaReadable;
    private List<int> MasakoReadable;

#if UNITY_EDITOR
    public TextAsset yamlJunna;
    public TextAsset yamlMasako;

    [ContextMenu("读取yaml")]
    public void ReadYamlAndApply()
    {
        Deserializer read = new();
        Junna = read.Deserialize<Timeline>(yamlJunna.text);
        Masako = read.Deserialize<Timeline>(yamlMasako.text);
       
    }
#endif

    private void Awake()
    {
        Application.targetFrameRate = 60;
        
        //先停止播放
        videoPlayer.Stop();
       
       
       
        }

    private void Start()
    {
        //游戏初始化（涉及到UI根据显示的分辨率变化位置，只能放到sstart中）
        Initialization();
        //准备就绪，开始播放
        videoPlayer.Play();
        }

    void Update()
    {
        

        if (!videoPlayer.isPlaying) return;

  

        if (videoPlayer.frame >= 5025)
        {
            metronome.StartPlay();
            return;
        }
        //提前显示下一个（如果在时间区间较短）
        ShowAheadForJunna();
        ShowAheadForMasako();

       

#if UNITY_EDITOR
        //debug用，记录时间与帧数
        frameCount.text = string.Format("{0}\n{1}", videoPlayer.frame.ToString(), ((int)videoPlayer.time).ToString());
        //节奏判断，并更新下一个要判定的音符
        Rhythm();
        //根据视频速度提高或者降低判定块的移动速度
        panDingSquares[0].OnlyForEditor(videoPlayer);
        panDingSquares[1].OnlyForEditor(videoPlayer);
#endif
    }

    /// <summary>
    /// 游戏初始化
    /// </summary>
    private void Initialization()
    {
        //把友好的时间转化为电脑可读的（视频帧数 视频是60fps的录屏emmmmmmm）
        //并且进行每一组的区分
        ConvertFriendlyToReadable();


        //初始化Junna大镲的音符
        //Junna 除了第一个是四分音符（有线），第四小节八分音符（有点） ，最后一个八分音符（有点）以外，其余的都是八分音符（有线）
        //所以，notesPoolForJunna中，第一个是四分音符（有线），第二个是八分音符（有点），剩下八个都是八分音符（有线） 
        //Masaka就直接八个八分音符了。（junna十个）
        notesPoolForJunna.Add(Instantiate(quarterNoteWithLine.gameObject).GetComponent<Notes>());
        notesPoolForJunna[^1].Initialization(0);
        notesPoolForJunna.Add(Instantiate(eighthNoteWithPoint.gameObject).GetComponent<Notes>());
        notesPoolForJunna[^1].Initialization(0);
        for (int i = 0; i < 8; i++)
        {
            notesPoolForJunna.Add(Instantiate(eighthNoteWithLine.gameObject).GetComponent<Notes>());
            notesPoolForMasako.Add(Instantiate(eighthNote.gameObject).GetComponent<Notes>());

            //初始化音符
            notesPoolForJunna[^1].Initialization(0);
            notesPoolForMasako[^1].Initialization(1);
        }
        
        //判定方块回车设定
        panDingSquares[0].SetEnterAndEnd(GetUIToWorldPos(panDingsInitialLocation[0]),
            GetUIToWorldPos(JunnaNoteLocation[^1]) + Vector2.right * 0.4f);
        panDingSquares[1].SetEnterAndEnd(GetUIToWorldPos(panDingsInitialLocation[1]),
            GetUIToWorldPos(MasakoNoteLocation[^1]) + Vector2.right * 0.4f);

        
    }
    /// <summary>
    /// 将有好的时间线转化为电脑可以用的（视频帧数）
    /// </summary>
    private void ConvertFriendlyToReadable()
    {
        JunnaReadable = new List<int>();
        MasakoReadable = new List<int>();


        //将友好型时间转化为可读的以 1帧 单位的可读时间
        for (int i = 0; i < Junna.Time.Length; i++)
        {
            //按照冒号分开。长度为4(有的是5）. 0 =1舍弃 =2有改动 .1是分钟（1min=60s=3600)  2是秒（1s=60） 3则可以视为帧数
            string[] fix = Junna.Time[i].Split(':');

            //lag * 10对于整体的滞后性进行修复
            JunnaReadable.Add(int.Parse(fix[3]) + int.Parse(fix[2]) * 60 + int.Parse(fix[1]) * 3600 + lag * 10);
          
            //按照预制的分隔符（fix 4 = D）记录音符间隔分组符号
            if (fix.Length == 5)
            {
                JunnaInterval.Add(JunnaReadable.Count - 1);
            }
        }

        //将友好型时间转化为可读的以 1帧 单位的可读时间
        for (int i = 0; i < Masako.Time.Length; i++)
        {
            //按照冒号分开。长度为4(有的是5）. 0 =1舍弃 =2有改动 .1是分钟（1min=60s=3600)  2是秒（1s=60） 3则可以视为帧数
            string[] fix = Masako.Time[i].Split(':');

            //要对masako进行修建，又不舍得删掉那些时间点，所以用这个了
            if (int.Parse(fix[0]) == 1)
            {
                continue;
                
            }
            
            //lag * 10对于整体的滞后性进行修复
            MasakoReadable.Add(int.Parse(fix[3]) + int.Parse(fix[2]) * 60 + int.Parse(fix[1]) * 3600 + lag * 10);
         
            //按照预制的分隔符（fix 4 = D）记录音符间隔分组符号
            if (fix.Length == 5)
            {
                MasakoInterval.Add(MasakoReadable.Count - 1);
            }
        }
        
        //节省内存，将友好型的时间线消除
        Junna.Time = null;
        Masako.Time = null;
    }

    
    
    
    /// <summary>
    /// 提前显示下一个（如果在时间区间较短）
    /// </summary>
    private void ShowAheadForJunna()
    {
        //Junna 除了第一个是四分音符（有线），第四小节八分音符（有点） ，最后一个八分音符（有点）以外，其余的都是八分音符（有线）
        //所以，notesPoolForJunna中，第一个是四分音符（有线），第二个是八分音符（有点），剩下八个都是八分音符（有线） 

        //按照预定好的分组，把池子里的音符拿出来
        //第一组单独的
        // notesPoolForJunna[2].position.y  < -50f 防止多次调用
        if (whichIntervalToUseForJunna == 0) 
        {
            if (notesPoolForJunna[2].GetTransform().position.y < -50f)
            {
                //开头的四分音符（有线）
                notesPoolForJunna[0].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[0]);
                //中间的正常八分音符（有线） 
                notesPoolForJunna[2].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[1]);
                notesPoolForJunna[3].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[2]);
                //第四小节的八分音符（有点）
                notesPoolForJunna[1].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[3]);

                //让判定滑块移动
                MoveSquare(4, (int)videoPlayer.frame,0,0, JunnaReadable, JunnaNoteLocation, JunnaInterval,
                    whichIntervalToUseForJunna, 0);

                whichIntervalToUseForJunna++;

            }
            
            Debug.Log(GetUIToWorldPos(JunnaNoteLocation[0]));
           
            return;
        }

        
        //最后一组单独的
        // notesPoolForJunna[2].position.y < -50f 防止多次调用(不过该return的还是得return，后面的不算了，音符消除也单独弄一个）
        if (videoPlayer.frame >= JunnaReadable[JunnaInterval[^2] + 1] - 60)
        {
            if (notesPoolForJunna[2].GetTransform().position.y < -50f)
            {
                Debug.Log("W");
            
                //算出来要几个八分音符（从0开始）
                int number = JunnaInterval[whichIntervalToUseForJunna] - JunnaInterval[whichIntervalToUseForJunna - 1] - 1;

                //按照数目要求，把八分音符放上去
                for (int i = 0; i < number; i++)
                {
                    notesPoolForJunna[2 + i].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[i]);
                }

                //第四小节的八分音符（有点）
                notesPoolForJunna[1].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[number]);
            
                //让判定滑块移动 +1：从零开始
                MoveSquare(number + 1,(int)videoPlayer.frame,0,0,JunnaReadable,JunnaNoteLocation,JunnaInterval,whichIntervalToUseForJunna,0);
            }
          
            return;
        }


        /*我在这讲两句
         * JunnaInterval[whichIntervalToUseForJunna - 1] + 1 ： 定位到本组的第一个，即上一组的结尾然后往后推算了一个
         * -60：比本组第一个提前60帧（视频帧率）出来
         */
        // notesPoolForJunna[2].position.y < -50f 防止多次调用
        if (videoPlayer.frame >= JunnaReadable[JunnaInterval[whichIntervalToUseForJunna - 1] + 1] - 60 &&   notesPoolForJunna[2].GetTransform().position.y < -50f)
        {
            //这里的话就全都是八分音符（有线） 
            //算出来要几个八分音符
            int number = JunnaInterval[whichIntervalToUseForJunna] - JunnaInterval[whichIntervalToUseForJunna - 1];
//按照数目要求，把八分音符放上去
            for (int i = 0; i < number; i++)
            {
                notesPoolForJunna[2 + i].GetTransform().position = GetUIToWorldPos(JunnaNoteLocation[i]);
            }

            //让判定滑块移动
            MoveSquare(number,(int)videoPlayer.frame,0,0,JunnaReadable,JunnaNoteLocation,JunnaInterval,whichIntervalToUseForJunna,0);

            //准备进行下一个了
            //最后一个，不再增加数值，防止数组溢出
            if (whichIntervalToUseForJunna != JunnaInterval.Count - 1) whichIntervalToUseForJunna++;
            return;
        }


        //用于重置本组的音符，以备下一组（whichIntervalToUseForJunna这一组的时候加了一，要减去）
        //本组超时1s后重置
        if (videoPlayer.frame >= JunnaReadable[JunnaInterval[whichIntervalToUseForJunna - 1]] + 60)
        {
            foreach (var VARIABLE in notesPoolForJunna)
            {
                VARIABLE.BackToInitialNotePoint();
            }
            
           
        }
    }

 
  [Obsolete("适用于亚组，但是现在亚组取消，所以可能不稳定",false)]
    private void ShowAheadForMasako()
    {
        //Masako 除了第一个是四分音符（有线），第四小节八分音符（有点） ，最后一个八分音符（有点）以外，其余的都是八分音符（有线）
        //所以，notesPoolForMasako中，第一个是四分音符（有线），第二个是八分音符（有点），剩下八个都是八分音符（有线） 
        //第一组单独的
        // notesPoolForMasako[0].position.y < -50f 防止多次调用，该return还得return
        if (whichIntervalToUseForMasako == 0 )
        {
            //这里进行的是多次重复调用，为的就是把所有的亚组给显示出来
            //现在不用多次重复调用了。没有亚组了
         if(notesPoolForMasako[0].GetTransform().position.y < -50f) HowMasakoNoteShow(MasakoInterval[0] + 1, true);
            return;
        }
     
       
        
        /*我在这讲两句
         * MasakoInterval[whichIntervalToUseForMasako - 1] + 1 ： 定位到本组的第一个，即上一组的结尾然后往后推算了一个
         * -60：比本组第一个提前60帧（视频帧率）出来
         */
        if (videoPlayer.frame >= MasakoReadable[MasakoInterval[whichIntervalToUseForMasako - 1] + 1] - 60)
        {
            //这里进行的是多次重复调用，为的就是把所有的亚组给显示出来
            //现在不用多次重复调用了。没有亚组了
            if(notesPoolForMasako[0].GetTransform().position.y < -50f) HowMasakoNoteShow(MasakoInterval[whichIntervalToUseForMasako] -
                                                                          MasakoInterval[whichIntervalToUseForMasako - 1]);
            return;
            
        }
    

        //用于重置本组的音符，以备下一组（whichIntervalToUseForMasako这一组的时候加了一，要减去）
        //本组超时1s后重置
        //notesPoolForMasako[0].GetTransform().position.y > -50防止多次反复调用
        if (videoPlayer.frame >= MasakoReadable[MasakoInterval[whichIntervalToUseForMasako - 1]] + 60 && notesPoolForMasako[0].GetTransform().position.y > -50f)
        {
            foreach (var VARIABLE in notesPoolForMasako)
            {
                VARIABLE.BackToInitialNotePoint();
            }

            //亚组重置
            //subGroupToUse = 0;
        }
    }

    
    /// <summary>
    /// 判定节奏（要求游戏帧数大于等于60）（仅Editor）
    /// </summary>
    private void Rhythm()
    {
        //帧数匹配，节奏对上，那个判定的圆形移动到判定线，且中心与判定线重合
        if ((int)videoPlayer.frame == MasakoReadable[timePointToUse[1]])
        {

           go.Rotate(Vector3.forward, 30f); //到时候的判定 debug务必要保证游戏帧数大于60


            //准备读取下一个时间点
            if (timePointToUse[1] != MasakoReadable.Count - 1) timePointToUse[1]++;
        }


        //帧数匹配，节奏对上，那个判定的圆形移动到判定线，且中心与判定线重合
        if ((int)videoPlayer.frame == JunnaReadable[timePointToUse[0]])
        {

            //  go.Rotate(Vector3.forward, 30f); //到时候的判定 debug务必要保证游戏帧数大于60
            

            //准备读取下一个时间点
            if (timePointToUse[0] != JunnaReadable.Count - 1) timePointToUse[0]++;
        }
    }
 
    /// <summary>
  /// 让判定方块移动（性能消耗大：Editor模式下有个Debug）
  /// </summary>
  /// <param name="noteCount">这一组（亚组）音符的数量</param>
  /// <param name="initialFrame">为这一组（亚组）的第一个音符的触碰设定初始视频帧率（计算时间，一般是这一组或亚组显示时的视频帧数）</param>
  /// <param name="subGroupIndex">第几个（完整的）亚组，Junna = 0，不分亚组也为0 有亚组的话，从1开始</param>
  /// <param name="leftNotes">所有的完整亚组弄完之后，剩下的不足八个的，Junna = 0，仍然是完整的亚组，也为0</param>
    private void MoveSquare(int noteCount,int initialFrame,int subGroupIndex,int leftNotes,List<int> characterReadable,RectTransform[] characterNoteLocation,List<int> characterInterval,int whichIntervalToUseForCharacter,int character)
    { 
       
        //为判定方块移动提前弄好数组的长度
        int[] targetFrame = new int[noteCount];
        int[] myFrame = new int[noteCount];
        Vector2[] targetLocation = new Vector2[noteCount];
        Vector2[] myLocation = new Vector2[noteCount];
      
        //第一个的位置一定是初始位置

        //根据是不是第一组，分别处理（防止第一组-1爆掉数组）
        if (whichIntervalToUseForCharacter == 0)
        {
            myFrame[0] = 0;
            targetFrame[0] = characterReadable[0];
            //防止第一组爆数组
            //先把第一组第一个跳过去，之后再添加
            for (int i = 1; i < noteCount; i++)
            {
                targetFrame[i] = characterReadable[i];
                //把上一个音符的视频帧数当作滑块自己的帧数
                myFrame[i] = characterReadable[i - 1];
                myLocation[i] = GetUIToWorldPos(characterNoteLocation[i - 1]);
                targetLocation[i] = GetUIToWorldPos(characterNoteLocation[i]);
            }

            //补上第一个
            targetFrame[0] = characterReadable[0];
            targetLocation[0] = GetUIToWorldPos(characterNoteLocation[0]);
        }
        else
        { 
          //正常处理计算速度
            for (int i = 0; i < noteCount; i++)
            {
                targetFrame[i] = characterReadable[i + 1 + characterInterval[whichIntervalToUseForCharacter - 1]];
                //把上一个音符的视频帧数当作滑块自己的帧数
                myFrame[i] = characterReadable[i + characterInterval[whichIntervalToUseForCharacter - 1]];
                targetLocation[i] = GetUIToWorldPos(characterNoteLocation[i]);
              if(i >= 1)  myLocation[i] = GetUIToWorldPos(characterNoteLocation[i - 1]);
            }
        }
     
        //第一个修正
        myFrame[0] = initialFrame;
        myLocation[0] =GetUIToWorldPos(panDingsInitialLocation[character]);
        
        
        //该让判定方块移动了
        panDingSquares[character].SetVelocity(targetFrame,myFrame,targetLocation,myLocation);
    }





    /// <summary>
    /// Masako的音符怎么显示
    /// </summary>
    /// <param name="number">算出来一共要几个八分音符</param>
    /// <param name="isFirst">是第一个分组吗</param>
    [Obsolete("适用于亚组，但是现在亚组取消了",false)]
    private void HowMasakoNoteShow(int number,bool isFirst = false)
    {
        //这里的话就全都是八分音符
    
        switch (number)
        {
            //数目少于等于8（一共就8个坑），直接放上去
            case <= 8:
                
                //要移动方块了
                MoveSquare(number,(int)videoPlayer.frame,0,0,MasakoReadable,MasakoNoteLocation,MasakoInterval,whichIntervalToUseForMasako,1);
                
                for (int i = 0; i < number; i++)
                {
                    notesPoolForMasako[i].GetTransform().position = GetUIToWorldPos(MasakoNoteLocation[i]);
                }

                //准备进行下一个了
                //最后一个，不再增加数值，防止数组溢出
                //增加这个之后，也能终止ShowAheadForMasako对本函数的反复调用
                if (whichIntervalToUseForMasako != MasakoInterval.Count - 1) whichIntervalToUseForMasako++;
              
                break; 

            //数目大于8（一共就8个坑），8个8个的往上放
            default:

                Debug.LogError("亚组取消");
                break;
               
              
        }
    }

    private void Metronome()
    {
        
    }
        /// <summary>
        /// UI坐标转世界坐标（2d)
        /// </summary>
        /// <param name="uiObject"></param>
        /// <returns></returns>
        private Vector2 GetUIToWorldPos(RectTransform uiObject)
        {
            Vector2 ptScreen = cameraUI.WorldToScreenPoint(uiObject.position);
            return cameraUI.ScreenToWorldPoint(ptScreen);
        }
    
}