using System;
using UnityEngine;

namespace ChickenInvader
{
    public sealed class Stage1Launcher : MonoBehaviour
    {
        private void Start()
        {
            try
            {
                var manager = GameManager.Instance;
                if (manager == null) throw new InvalidOperationException("Place GameManager in the MainMenu scene.");
                // shortcut: auto-start the prototype; replace this launcher with menu buttons in B4.
                Debug.Log("Stage 1 MainMenu shell: starting a new run automatically.", this);
                manager.StartNewRun();
                manager.LoadLevel(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }
    }
}
