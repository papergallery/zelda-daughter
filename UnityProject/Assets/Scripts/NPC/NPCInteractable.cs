using System.Collections;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.NPC
{
    public class NPCInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NPCData _data;
        [SerializeField] private float _interactionRange = 2f;
        [SerializeField] private float _turnSpeed = 5f;
        [SerializeField] private IconBubble _iconBubble;
        [SerializeField] private IconResponsePanel _responsePanel;

        private bool _isTalking;
        private bool _responseReceived;

        public string InteractionPrompt => _data != null ? _data.NpcName : "NPC";
        public Transform InteractionPoint => transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.NPC;

        public bool CanInteract() => !_isTalking;

        public void Interact(GameObject actor)
        {
            if (_isTalking) return;
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

            // Показать последовательность иконок
            if (_iconBubble != null && _data != null && _data.IconSequence != null && _data.IconSequence.Length > 0)
            {
                _iconBubble.ShowSequence(_data.IconSequence, _data.IconDisplayInterval);
                yield return new WaitForSeconds(_data.IconSequence.Length * _data.IconDisplayInterval);
            }

            // Показать панель ответов и ждать выбора
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
