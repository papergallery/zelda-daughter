using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Progression;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.NPC
{
    public class WeaponTrainerNPC : MonoBehaviour
    {
        [SerializeField] private WeaponType _taughtWeapon;
        [SerializeField] private TrainingDummy _dummy;
        [SerializeField] private WeaponProficiency _playerProficiency;
        [SerializeField] private TrainingDummyConfig _config;
        [SerializeField] private Transform _trainingPosition;

        private bool _trainingInProgress;
        private bool _subscribed;

        public void StartTraining()
        {
            if (_trainingInProgress) return;

            _trainingInProgress = true;
            _dummy.gameObject.SetActive(true);
            _dummy.StartSession();

            TrainingDummy.OnTrainingComplete += HandleTrainingComplete;
            _subscribed = true;
        }

        private void HandleTrainingComplete(TrainingDummy dummy)
        {
            if (dummy != _dummy) return;

            _playerProficiency.AddExperience(_taughtWeapon, _config.XpReward);
            SpeechBubbleManager.Say(_config.CompletionReply);

            _dummy.gameObject.SetActive(false);
            _trainingInProgress = false;

            Unsubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            TrainingDummy.OnTrainingComplete -= HandleTrainingComplete;
            _subscribed = false;
        }
    }
}
