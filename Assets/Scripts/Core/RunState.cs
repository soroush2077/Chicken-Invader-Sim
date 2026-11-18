using System;
using UnityEngine;

namespace ChickenInvader
{
    [Serializable]
    public sealed class RunState
    {
        [SerializeField] private int levelIndex;
        [SerializeField] private int roundIndex;
        [SerializeField] private int lives = 3;
        [SerializeField] private int score;
        [SerializeField] private int bombs = 2;
        [SerializeField] private int weaponPower = 1;

        public int LevelIndex => levelIndex;
        public int RoundIndex => roundIndex;
        public int Lives => lives;
        public int Score => score;
        public int Bombs => bombs;
        public int WeaponPower => weaponPower;
        public bool IsGameOver => lives == 0;

        public event Action Changed;

        public void Reset()
        {
            levelIndex = 0;
            roundIndex = 0;
            lives = 3;
            score = 0;
            bombs = 2;
            weaponPower = 1;
            Changed?.Invoke();
        }

        internal void SetProgress(int newLevelIndex, int newRoundIndex)
        {
            if (newLevelIndex < 0) throw new ArgumentOutOfRangeException(nameof(newLevelIndex));
            if (newRoundIndex < 0) throw new ArgumentOutOfRangeException(nameof(newRoundIndex));
            if (levelIndex == newLevelIndex && roundIndex == newRoundIndex) return;
            levelIndex = newLevelIndex;
            roundIndex = newRoundIndex;
            Changed?.Invoke();
        }

        internal void AddScore(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            score = checked(score + amount);
            Changed?.Invoke();
        }

        internal bool TakePlayerHit()
        {
            if (IsGameOver) return false;
            lives--;
            weaponPower = Mathf.Max(1, weaponPower - 1);
            Changed?.Invoke();
            return true;
        }

        internal bool UpgradeWeapon()
        {
            if (IsGameOver || weaponPower >= 3) return false;
            weaponPower++;
            Changed?.Invoke();
            return true;
        }

        internal bool SpendBomb()
        {
            if (IsGameOver || bombs <= 0) return false;
            bombs--;
            Changed?.Invoke();
            return true;
        }

#if UNITY_EDITOR
        internal static void CheckScenePersistence()
        {
            var test = new RunState();
            test.AddScore(350);
            test.UpgradeWeapon();
            test.UpgradeWeapon();
            test.TakePlayerHit();
            test.SpendBomb();
            test.SetProgress(1, 0);
            Debug.Assert(test.LevelIndex == 1 && test.RoundIndex == 0 && test.Lives == 2 &&
                test.Score == 350 && test.Bombs == 1 && test.WeaponPower == 2,
                "Changing levels must preserve the player's run values.");
            test.SetProgress(2, 0);
            Debug.Assert(test.Lives == 2 && test.Score == 350 && test.Bombs == 1 && test.WeaponPower == 2);
            Debug.Log("Scene progress rule checks completed; inspect the Console for assertion failures.");
        }

        internal static void CheckDamageRules()
        {
            var test = new RunState { weaponPower = 3 };
            int changes = 0;
            test.Changed += () => changes++;
            Debug.Assert(test.TakePlayerHit() && test.Lives == 2 && test.WeaponPower == 2);
            Debug.Assert(test.TakePlayerHit() && test.Lives == 1 && test.WeaponPower == 1);
            Debug.Assert(test.TakePlayerHit() && test.IsGameOver && test.WeaponPower == 1);
            Debug.Assert(!test.TakePlayerHit() && test.Lives == 0 && changes == 3,
                "A defeated run must not lose more lives or emit more changes.");
            test.Reset();
            Debug.Assert(test.Lives == 3 && !test.IsGameOver && test.WeaponPower == 1);
            Debug.Log("Damage rule checks completed; inspect the Console for assertion failures.");
        }

        internal static void CheckWeaponRules()
        {
            var test = new RunState();
            int changes = 0;
            test.Changed += () => changes++;
            Debug.Assert(test.UpgradeWeapon() && test.WeaponPower == 2);
            Debug.Assert(test.UpgradeWeapon() && test.WeaponPower == 3);
            Debug.Assert(!test.UpgradeWeapon() && test.WeaponPower == 3 && changes == 2);
            Debug.Assert(test.SpendBomb() && test.SpendBomb() && !test.SpendBomb() && test.Bombs == 0);
            test.TakePlayerHit();
            Debug.Assert(test.WeaponPower == 2);
            test.TakePlayerHit();
            test.TakePlayerHit();
            Debug.Assert(!test.UpgradeWeapon() && !test.SpendBomb());
            test.Reset();
            Debug.Assert(test.Bombs == 2 && test.WeaponPower == 1);
            Debug.Log("Weapon rule checks completed; inspect the Console for assertion failures.");
        }
#endif
    }
}
