using System.Collections;
using UnityEngine;

namespace Invector.vCharacterController.vActions
{
    using System.Collections.Generic;
    using System.Linq;
    using vCharacterController;
    [vClassHeader("GENERIC ACTION", "Use the vTriggerGenericAction to trigger a simple animation.", iconName = "triggerIcon")]
    public class vGenericAction : vActionListener
    {
        [Tooltip("Tag of the object you want to access")]
        public string actionTag = "Action";
        [Tooltip("Use root motion of the animation")]
        public bool useRootMotion = true;

        [Header("--- Debug Only ---")]
        public vTriggerGenericAction triggerAction;
        [Tooltip("Check this to enter the debug mode")]
        public bool debugMode;
        public bool canTriggerAction;
        public bool triggerActionOnce;
        public Camera mainCamera;
        public UnityEngine.Events.UnityEvent OnStartAction;
        public UnityEngine.Events.UnityEvent OnCancelAction;
        public UnityEngine.Events.UnityEvent OnEndAction;

        internal vThirdPersonInput tpInput;

        float _currentInputDelay;
        bool _playingAnimation;
        Vector3 screenCenter;
        public Dictionary<Collider, vTriggerGenericAction> actions;
        float timeInTrigger;

        private void Awake()
        {
            actionEnter = true;
            actionStay = true;
            actionExit = true;
            actions = new Dictionary<Collider, vTriggerGenericAction>();
        }

        protected override void Start()
        {
            base.Start();
            tpInput = GetComponent<vThirdPersonInput>();
            screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
            if (!mainCamera) mainCamera = Camera.main;

        }

        protected virtual bool inActionAnimation
        {
            get
            {
                return !string.IsNullOrEmpty(triggerAction.playAnimation)
                    && tpInput.cc.baseLayerInfo.IsName(triggerAction.playAnimation);
            }
        }

        public virtual bool playingAnimation
        {
            get
            {
                if (triggerAction == null) return _playingAnimation = false;
                if (!_playingAnimation && inActionAnimation)
                {
                    _playingAnimation = true;
                    DisablePlayerGravityAndCollision();
                }
                else
                if (_playingAnimation && !inActionAnimation)
                {
                    _playingAnimation = false;
                }
                return _playingAnimation;
            }
            protected set
            {
                _playingAnimation = true;
            }
        }

        public virtual bool actionConditions
        {
            get
            {
                return (!tpInput.cc.isJumping || !tpInput.cc.customAction || (canTriggerAction && !triggerActionOnce) || !playingAnimation) && !tpInput.cc.animator.IsInTransition(0);
            }
        }

        public override void OnActionEnter(Collider other)
        {
            if (other.gameObject.CompareTag(actionTag) && !actions.ContainsKey(other))
            {
                vTriggerGenericAction triggerAction = other.GetComponent<vTriggerGenericAction>();
                if (triggerAction && triggerAction.enabled) actions.Add(other, triggerAction);
            }
        }

        public override void OnActionExit(Collider other)
        {
            if (other.gameObject.CompareTag(actionTag) && actions.ContainsKey(other))
            {
                actions[other].OnPlayerExit.Invoke();
                triggerActionOnce = false;
                canTriggerAction = false;
                actions.Remove(other);
            }
        }

        public override void OnActionStay(Collider other)
        {
            if (other.gameObject.CompareTag(actionTag))
            {
                timeInTrigger = .5f;
            }
        }

        protected virtual void CheckForTriggerAction()
        {
            if (actions.Count == 0 && !triggerAction) return;
            vTriggerGenericAction _triggerAction = GetNearAction();

            if (!_triggerAction || !_triggerAction.enabled || !_triggerAction.gameObject.activeInHierarchy)
            {
                return;
            }

            var dist = Vector3.Distance(transform.forward, _triggerAction.transform.forward);

            if (!_triggerAction.activeFromForward || dist <= 0.8f)
            {
                if (!tpInput.cc.customAction && !canTriggerAction)
                {
                    triggerAction = _triggerAction;
                    canTriggerAction = true;

                    ///Change method to OnValidateAction
                    triggerAction.OnPlayerEnter.Invoke();
                }
            }
            else if (canTriggerAction)
            {
                if (triggerAction != null)
                {
                    triggerAction.OnPlayerExit.Invoke();
                    ///Change method to OnInvalidateAction
                }
                canTriggerAction = false;
            }
            TriggerActionInput();
        }

        private void Update()
        {
            if (!mainCamera) mainCamera = Camera.main;
            if (!mainCamera) return;          
            AnimationBehaviour();
            CheckForTriggerAction();
            if(!doingAction)
            {
                if (timeInTrigger <= 0)
                {
                    actions.Clear();
                    triggerAction = null;
                    triggerActionOnce = false;
                    canTriggerAction = false;
                }
                else timeInTrigger -= Time.deltaTime;
            }            
        }

        protected virtual void TriggerActionInput()
        {
            if (triggerAction == null || !triggerAction.gameObject.activeInHierarchy) return;

            if (canTriggerAction)
            {
                if (triggerAction.inputType == vTriggerGenericAction.InputType.AutoAction && actionConditions)
                {
                    TriggerAnimation();
                }
                else if (triggerAction.inputType == vTriggerGenericAction.InputType.GetButtonDown && actionConditions)
                {
                    if (triggerAction.actionInput.GetButtonDown())
                    {
                        TriggerAnimation();
                    }
                }
                else if (triggerAction.inputType == vTriggerGenericAction.InputType.GetDoubleButton && actionConditions)
                {
                    if (triggerAction.actionInput.GetDoubleButtonDown(triggerAction.doubleButtomTime))
                        TriggerAnimation();
                }
                else if (triggerAction.inputType == vTriggerGenericAction.InputType.GetButtonTimer)
                {
                    if (_currentInputDelay <= 0)
                    {
                        // this mode will animate while you press the button and call the OnEndAction once you finish pressing the button
                        if (triggerAction.doActionWhilePressingButton)
                        {
                            var up = false;
                            var t = 0f;
                            // call the OnEndAction after the buttomTimer 
                            if (triggerAction.actionInput.GetButtonTimer(ref t, ref up, triggerAction.buttonTimer))
                            {
                                triggerAction.OnFinishActionInput.Invoke();

                                ResetActionState();
                                ResetTriggerSettings();
                            }
                            // trigger the animation when you start pressing the action button
                            if (triggerAction && triggerAction.actionInput.inButtomTimer)
                            {
                                triggerAction.UpdateButtonTimer(t);
                                TriggerAnimation();
                            }
                            // call OnCancelActionInput if the button is released
                            if (up && triggerAction)
                            {
                                triggerAction.OnCancelActionInput.Invoke();
                                _currentInputDelay = triggerAction.inputDelay;
                                triggerAction.UpdateButtonTimer(0);
                                ResetActionState();
                                ResetTriggerSettings();
                            }
                        }
                        // this mode will call the animation and event only when the buttonTimer finished 
                        else
                        {
                            if (triggerAction.actionInput.GetButtonTimer(triggerAction.buttonTimer))
                            {
                                TriggerAnimation();

                                if (playingAnimation)
                                {
                                    if (debugMode) Debug.Log("call OnFinishInput Event");
                                    triggerAction.OnFinishActionInput.Invoke();
                                }
                            }
                        }
                    }
                    else
                    {
                        _currentInputDelay -= Time.deltaTime;
                    }
                }
            }
        }

        protected virtual void TriggerAnimation()
        {
            if (triggerActionOnce || playingAnimation) return;

            OnDoAction.Invoke(triggerAction);
            doingAction = true;
            if (debugMode) Debug.Log("TriggerAnimation", gameObject);

            if (triggerAction.animatorActionState != 0)
            {
                if (debugMode) Debug.Log("Applied ActionState: " + triggerAction.animatorActionState, gameObject);
                tpInput.cc.SetActionState(triggerAction.animatorActionState);
            }

            // trigger the animation behaviour & match target
            if (!string.IsNullOrEmpty(triggerAction.playAnimation))
            {
                playingAnimation = true;
                tpInput.cc.animator.CrossFadeInFixedTime(triggerAction.playAnimation, 0.1f);    // trigger the action animation clip
                if (!string.IsNullOrEmpty(triggerAction.customCameraState))
                    tpInput.ChangeCameraState(triggerAction.customCameraState);                 // change current camera state to a custom
            }

            // trigger OnDoAction Event, you can add a delay in the inspector   
            StartCoroutine(triggerAction.OnDoActionDelay(gameObject));
            //if (TriggerActionOnce()) 
            triggerActionOnce = true;

            // destroy the triggerAction if checked with destroyAfter
            if (triggerAction.destroyAfter)
                StartCoroutine(DestroyActionDelay(triggerAction));
        }

        bool animationStarted;
        protected virtual void AnimationBehaviour()
        {
            if (playingAnimation)
            {
                if (!animationStarted) animationStarted = true;

                OnStartAction.Invoke();
                triggerAction.OnStartAnimation.Invoke();

                if (triggerAction.matchTarget != null)
                {
                    if (debugMode) Debug.Log("Match Target...");
                    // use match target to match the Y and Z target 
                    tpInput.cc.MatchTarget(triggerAction.matchTarget.transform.position, triggerAction.matchTarget.transform.rotation, triggerAction.avatarTarget,
                        new MatchTargetWeightMask(triggerAction.matchPos, triggerAction.matchRot), triggerAction.startMatchTarget, triggerAction.endMatchTarget);
                }

                if (triggerAction.useTriggerRotation)
                {
                    if (debugMode) Debug.Log("Rotate to Target...");
                    // smoothly rotate the character to the target
                    var newRot = new Vector3(transform.eulerAngles.x, triggerAction.transform.eulerAngles.y, transform.eulerAngles.z);
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(newRot), tpInput.cc.animator.GetCurrentAnimatorStateInfo(0).normalizedTime);

                }

                if (triggerAction.inputType != vTriggerGenericAction.InputType.GetButtonTimer && tpInput.cc.animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= triggerAction.endExitTimeAnimation)
                {
                    if (debugMode) Debug.Log("Finish Animation");

                    // triggers the OnEndAnimation Event
                    triggerAction.OnEndAnimation.Invoke();
                    // reset GenericAction variables so you can use it again
                    ResetTriggerSettings();
                }
            }
            else if (doingAction && animationStarted)
            {
                // when using a GetButtonTimer the ResetTriggerSettings will be automatically called at the end of the timer or by releasing the input
                if (triggerAction != null && triggerAction.inputType == vTriggerGenericAction.InputType.GetButtonTimer) return;

                if (debugMode) Debug.Log("Force ResetTriggerSettings");
                // triggers the OnEndAnimation Event
                triggerAction.OnEndAnimation.Invoke();
                ResetTriggerSettings();
            }
        }

        //private bool TriggerActionOnce()
        //{
        //    // bool to limit the autoAction run just once
        //    return triggerAction.inputType == vTriggerGenericAction.InputType.AutoAction || triggerAction.inputType == vTriggerGenericAction.InputType.GetButtonTimer || triggerAction.destroyAfter;
        //}

        public void ResetActionState()
        {
            if (triggerAction && triggerAction.resetAnimatorActionState)
                tpInput.cc.SetActionState(0);
        }

        public virtual void ResetTriggerSettings()
        {
            if (debugMode) Debug.Log("Reset Trigger Settings");

            animationStarted = false;
            // reset player gravity and collision
            EnablePlayerGravityAndCollision();
            // reset the Animator parameter ActionState back to 0 
            ResetActionState();
            // reset the CameraState to the Default state
            tpInput.ResetCameraState();
            // reset canTriggerAction so you can trigger another action
            canTriggerAction = false;
            triggerAction = null;
            // reset triggerActionOnce so you can trigger again
            triggerActionOnce = false;
            doingAction = false;
        }

        public virtual void DisablePlayerGravityAndCollision()
        {
            if (triggerAction && triggerAction.disableGravity)
            {
                if (debugMode) Debug.Log("Disable Player's Gravity");
                tpInput.cc._rigidbody.useGravity = false;
                tpInput.cc._rigidbody.linearVelocity = Vector3.zero;
            }
            if (triggerAction && triggerAction.disableCollision)
            {
                if (debugMode) Debug.Log("Disable Player's Collision");
                tpInput.cc._capsuleCollider.isTrigger = true;
            }
        }

        public virtual void EnablePlayerGravityAndCollision()
        {
            //if (triggerAction && triggerAction.disableGravity)
            {
                if (debugMode) Debug.Log("Enable Player's Gravity");
                tpInput.cc._rigidbody.useGravity = true;
            }
            //if (triggerAction && triggerAction.disableCollision)
            {
                if (debugMode) Debug.Log("Enable Player's Collision");
                tpInput.cc._capsuleCollider.isTrigger = false;
            }
        }

        public virtual IEnumerator DestroyActionDelay(vTriggerGenericAction triggerAction)
        {
            var _triggerAction = triggerAction;
            yield return new WaitForSeconds(_triggerAction.destroyDelay);
            OnEndAction.Invoke();
            ResetTriggerSettings();
            Destroy(_triggerAction.gameObject);
        }

        protected vTriggerGenericAction GetNearAction()
        {
         
            float distance = Mathf.Infinity;
            vTriggerGenericAction targetAction = null;
          
            foreach (var key in actions.Keys)
            {
                if (key)
                {
                    var screenP =mainCamera? mainCamera.WorldToScreenPoint(key.transform.position): screenCenter;
                    if(mainCamera)
                    {
                        if ((screenP - screenCenter).magnitude < distance)
                        {
                            distance = (screenP - screenCenter).magnitude;
                            if (targetAction && targetAction != actions[key]) targetAction.OnPlayerExit.Invoke();
                            targetAction = actions[key];
                        }
                        else
                        {
                            actions[key].OnPlayerExit.Invoke();
                        }
                    }
                    else
                    {
                        if(!targetAction)
                        {
                            targetAction = actions[key];
                        }
                        else
                        {
                            actions[key].OnPlayerExit.Invoke();
                        }
                    }
                   
                }
                else
                {
                    actions.Remove(key);
                    return null;
                }
            }
           

            return targetAction;
        }
    }
}