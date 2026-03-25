using DiceBattler.Configs;
using DiceBattler.Runtime;
using UnityEngine;

namespace DiceBattler.Presentation
{
    public sealed class CombatSceneInstaller : MonoBehaviour
    {
        [SerializeField] private PrototypeContentSet contentSet;
        [SerializeField] private CombatHudPresenter hudPresenter;
        [SerializeField] private HeroPresenter heroPresenter;
        [SerializeField] private EnemyPresenter[] enemyPresenters;
        [SerializeField] private DiePresenter[] diePresenters;
        [SerializeField] private OverlayController overlayController;
        [SerializeField] private CombatFlowController flowController;

        public void Initialize(PrototypeContentSet injectedContentSet)
        {
            contentSet = injectedContentSet;
            if (flowController == null)
            {
                flowController = GetComponent<CombatFlowController>();
                if (flowController == null)
                {
                    flowController = gameObject.AddComponent<CombatFlowController>();
                }
            }

            flowController.Initialize(contentSet, hudPresenter, heroPresenter, enemyPresenters, diePresenters, overlayController);
        }
    }
}
