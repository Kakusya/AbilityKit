using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AbilityKit.Core.Eventing;
using AbilityKit.Modifiers;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.MyPractice
{
    public class MyPracticeGame : MonoBehaviour
    {
        [Header("场景对象关联")]
        public GameObject dummyVisual; // 假人方块模型

        // 1. 战斗数据层（纯 C#，完全沿用刚才控制台写的逻辑！）
        private TargetDummy _dummy;
        private EventBus _bus;
        private TriggerRunner<TriggerContext> _triggerRunner;
        private EventKey<DamageEvent> _damageEventKey;

        // 2. 当前正在跑的技能
        private IAbilityPipelineRun<MySkillContext> _currentRun;
        private MySkillPipeline _pipeline;

        private void Start()
        {
            // 初始化假人
            _dummy = new TargetDummy();

            // 如果没手动拖入方块，就用代码在 (0, 1, 3) 的位置原地造一个假人方块
            if (dummyVisual == null)
            {
                dummyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummyVisual.name = "TargetDummy_Cube";
                dummyVisual.transform.position = new Vector3(0, 1, 3);
                dummyVisual.GetComponent<Renderer>().material.color = Color.red;
            }

            // 初始化广播电台与触发器哨兵
            _bus = new EventBus();
            _triggerRunner = new TriggerRunner<TriggerContext>(_bus, new FunctionRegistry(), new ActionRegistry());
            _damageEventKey = new EventKey<DamageEvent>(StableStringId.Get("event:damage"));

            // 注册哨兵：单次伤害 >= 40 触发二次大爆炸！
            var trigger = new DelegateTrigger<DamageEvent, TriggerContext>(
                predicate: (evt, ctx) => evt.Amount >= 40f,
                actions: (evt, ctx) =>
                {
                    Debug.Log($"<color=yellow>★ [被动触发！] 伤害达标 ({evt.Amount} >= 40)！触发【烈焰爆裂】！</color>");
                    _dummy.TakeDamage(80f);

                    // 在 Unity 里把方块变成黄色表示二次大爆炸！
                    StartCoroutine(FlashDummyColor(Color.yellow, 0.4f));
                });

            _triggerRunner.Register(_damageEventKey, trigger);

            // 组装三连发火球管线
            _pipeline = new MySkillPipeline();
            _pipeline.AddPhase(new AbilityDelayPhase<MySkillContext>(0.3f)); // 前摇

            var barrage = new AbilityRepeatPhase<MySkillContext>(3);
            barrage.RepeatInterval = 0.15f;
            barrage.SetRepeatAction((ctx, index) =>
            {
                float damage = 20f + index * 15f; // 20, 35, 50
                Debug.Log($"<color=orange>[火球术] 第 {index + 1}/3 发打中假人！基础伤害: {damage}</color>");
                _dummy.TakeDamage(damage);

                // 让假人闪烁白光（受击反馈）
                StartCoroutine(FlashDummyColor(Color.white, 0.08f));

                // 广播伤害事件
                _bus.Publish(_damageEventKey, new DamageEvent(damage));
            });
            _pipeline.AddPhase(barrage);
            _pipeline.AddPhase(new AbilityDelayPhase<MySkillContext>(0.2f)); // 后摇
        }

        private void Update()
        {
            // 按空格键施放火球术！
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("<color=cyan>--> [按键] 空格键按下，开始施法！</color>");
                _currentRun = _pipeline.Start(new MySkillConfig(), new MySkillContext());
            }

            // 游戏引擎的心跳推进
            if (_currentRun != null && _currentRun.State == EAbilityPipelineState.Executing)
            {
                _currentRun.Tick(Time.deltaTime); // 把 Unity 的帧间隔时间传给管线
            }

            // 派发电台事件，驱动触发器！
            _bus?.Flush();
        }

        // 闪烁方块颜色的简单视觉效果
        private IEnumerator FlashDummyColor(Color flashColor, float duration)
        {
            if (dummyVisual == null) yield break;
            var renderer = dummyVisual.GetComponent<Renderer>();
            var originalColor = Color.red;
            renderer.material.color = flashColor;
            yield return new WaitForSeconds(duration);
            if (renderer != null) renderer.material.color = originalColor;
        }

        private void OnGUI()
        {
            // 在屏幕左上角显示操作提示和假人血量
            GUI.Box(new Rect(20, 20, 300, 100), "AbilityKit 打靶演练");
            GUI.Label(new Rect(30, 50, 280, 25), $"假人血量: {_dummy?.Hp ?? 0} / 1000");
            GUI.Label(new Rect(30, 75, 280, 25), "【按下空格键 (Space)】施放火球术");
        }
    }
}
