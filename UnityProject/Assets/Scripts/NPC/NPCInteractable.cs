using System.Collections;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.NPC
{
    public class NPCInteractable : MonoBehaviour, IInteractable
    {
        [Header("Legacy (NPCData — obsolete)")]
        [SerializeField] private NPCData _data;
        [SerializeField] private IconBubble _iconBubble;
        [SerializeField] private IconResponsePanel _responsePanel;

        [Header("Dialogue System")]
        [SerializeField] private NPCProfile _profile;
        [SerializeField] private NPCSpeechBubble _speechBubble;
        [SerializeField] private DialogueManager _dialogueManager;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = 2f;
        [SerializeField] private float _turnSpeed = 5f;

        private bool _isTalking;
        private bool _responseReceived;

        public string InteractionPrompt => _profile != null ? _profile.NpcName
            : (_data != null ? _data.NpcName : "NPC");
        public Transform InteractionPoint => transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.NPC;

        public bool CanInteract()
        {
            if (_isTalking) return false;
            // Не начинаем диалог, если DialogueManager уже занят другим NPC
            if (_dialogueManager != null && _dialogueManager.IsActive) return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract()) return;
            _isTalking = true;
            StartCoroutine(InteractionSequence(actor));
        }

        private IEnumerator InteractionSequence(GameObject actor)
        {
            // Плавный поворот к actor
            Vector3 directionToActor = (actor.transform.position - transform.position).normalized;
            directionToActor.y = 0f;

            if (directionToActor != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToActor);
                float angle = Quaternion.Angle(transform.rotation, targetRotation);

                while (angle > 5f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _turnSpeed);
                    angle = Quaternion.Angle(transform.rotation, targetRotation);
                    yield return null;
                }
                transform.rotation = targetRotation;
            }

            // Анимация взаимодействия на actor
            if (actor.TryGetComponent<Animator>(out var actorAnimator))
                actorAnimator.SetTrigger("Interact");

            // Новая диалоговая система
            if (_dialogueManager != null && _profile != null && _profile.DialogueTree != null)
            {
                if (_speechBubble == null)
                    _speechBubble = gameObject.AddComponent<NPCSpeechBubble>();
                _dialogueManager.StartDialogue(_profile, _speechBubble);
                yield return new WaitUntil(() => !_dialogueManager.IsActive);
                _isTalking = false;
                yield break;
            }
            else
            {
                Debug.LogWarning($"[ZD:NPC] {gameObject.name} диалог не запущен: dm={_dialogueManager != null} profile={_profile != null} tree={_profile?.DialogueTree != null} bubble={_speechBubble != null}");
            }

            // Fallback: старая логика с иконками (для обратной совместимости)
            if (_iconBubble != null && _data != null && _data.IconSequence != null && _data.IconSequence.Length > 0)
            {
                _iconBubble.ShowSequence(_data.IconSequence, _data.IconDisplayInterval);
                yield return new WaitForSeconds(_data.IconSequence.Length * _data.IconDisplayInterval);
            }

            if (_responsePanel != null && _data != null && _data.IconSequence != null && _data.IconSequence.Length > 0)
            {
                _responseReceived = false;
                _responsePanel.OnResponseSelected += HandleResponseSelected;
                _responsePanel.Show(_data.IconSequence);

                yield return new WaitUntil(() => _responseReceived);

                _responsePanel.OnResponseSelected -= HandleResponseSelected;
            }

            _isTalking = false;
        }

        private void HandleResponseSelected(int index)
        {
            _responseReceived = true;
        }
    }
}
