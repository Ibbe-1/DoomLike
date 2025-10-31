using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
using WMPLib;
using static DoomLike.Hitreg;
using Timer = System.Windows.Forms.Timer;

namespace DoomLike
{
    public class DoomLikeGame : Form
    {


        // HP manager
        private PlayerHP playerHP = new PlayerHP();

        // Game state
        private bool isGameOver = false;

        // Enum to track what the weapon is doing
        private enum WeaponState { Idle, Shooting, Reloading }

        // Current state of the weapon
        private WeaponState currentWeaponState = WeaponState.Idle;

        // Textures
        private Bitmap wallSprite, floorSprite, skybox;

        // Weapon sprites
        private Bitmap weaponIdle, weaponShoot1, weaponShoot2, weaponReload1, weaponReload2, currentWeaponSprite;

        // Sounds
        private SoundPlayer ShootSound, ReloadSound, death;
        private WindowsMediaPlayer bgMusicPlayer;

        // Enemy manager
        private EnemyManager enemyManager = new EnemyManager();

        // Level manager
        private LevelManager levelManager;

        // Renderer instance
        private Renderer renderer;

        // Stores all active bullets in the game
        private List<Bullet> bullets = new List<Bullet>();

        // Screen constants
        private const int ScreenWidth = 640, ScreenHeight = 480;

        // Player position and angle
        private double playerX = 3.5, playerY = 3.5, playerAngle = 0;

        // Level transition tracking
        private bool levelTransitioning = false;
        private int transitionTimer = 0;

        // Background music files
        private string music1;
        private string music2;
        // checks currently playing music
        private string currentMusic;

        // the int for our player, it will contain the HP values.
        private int playerHealth = 100;
        private int maxHealth = 100;

        public DoomLikeGame()
        {
            Text = "Doomlike";
            ClientSize = new Size(ScreenWidth, ScreenHeight);
            this.DoubleBuffered = true;

            // Initialize the level manager
            levelManager = new LevelManager();

            // Initialize the renderer
            renderer = new Renderer();

            // Load wall and floor sprites
            wallSprite = new Bitmap(Properties.Resources.WallTexture);
            floorSprite = new Bitmap(Properties.Resources.FloorTexture);
            skybox = new Bitmap(Properties.Resources.skybox);

            // Load weapon sprites
            weaponIdle = new Bitmap(Properties.Resources.WeaponIdle);
            weaponShoot1 = new Bitmap(Properties.Resources.shooting1);
            weaponShoot2 = new Bitmap(Properties.Resources.shooting2);
            weaponReload1 = new Bitmap(Properties.Resources.reload1);
            weaponReload2 = new Bitmap(Properties.Resources.reload2);
            currentWeaponSprite = weaponIdle;

            // Load sounds
            ShootSound = new SoundPlayer(Properties.Resources.ShootSound);
            ReloadSound = new SoundPlayer(Properties.Resources.ReloadSound);
            death = new SoundPlayer(Properties.Resources.DeathSound);

            // start background music
            StartBackgroundMusic();
            // registers damage to player
            PlayerDamage();

            // Spawn initial enemies for level 1
            levelManager.Reset(enemyManager);

            // Game loop timer
            Timer timer = new Timer { Interval = 20 };
            timer.Tick += (s, e) => GameLoop();

            timer.Start();
            this.KeyDown += KeyboardInputs;


        }

        private void StartBackgroundMusic()
        {
            music1 = Path.GetTempFileName() + ".wav";
            music2 = Path.GetTempFileName() + ".wav";

            // temp file for music 1
            using (var ms = Properties.Resources.BGmusic)
            using (var fs = new FileStream(music1, FileMode.Create, FileAccess.Write))
            {
                ms.CopyTo(fs);
            }
            // temp file for music 2
            using (var ms2 = Properties.Resources.BGmusic2)
            using (var fs2 = new FileStream(music2, FileMode.Create, FileAccess.Write))
            {
                ms2.CopyTo(fs2);
            }

            bgMusicPlayer = new WindowsMediaPlayer();
            bgMusicPlayer.settings.setMode("loop", true);

            // Start playing initial music based on level
            currentMusic = (levelManager.CurrentLevel > 3) ? music2 : music1;
            bgMusicPlayer.URL = currentMusic;
            bgMusicPlayer.controls.play();
        }

        private void PlayerDamage()
        {
            enemyManager.OnPlayerHit = (damage) =>
            {
                if (isGameOver) return; // prevent multiple calls after death

                playerHealth -= damage;
                // shows damage taken
                playerHP.ShowDamage(damage);

                var damageSound = new SoundPlayer(Properties.Resources.PlayerDamage);
                damageSound.Play();

                if (playerHealth <= 0)
                {
                    playerHealth = 0;
                    var deathSound = new SoundPlayer(Properties.Resources.PlayerDeath);
                    deathSound.Play();

                    isGameOver = true; // stop the game
                    GameOver();
                }
            };
        }

        private void GameLoop()
        {
            if (isGameOver) return;

            // Animate weapon frames
            if (currentWeaponState == WeaponState.Shooting)
                currentWeaponSprite = (currentWeaponSprite == weaponShoot1) ? weaponShoot2 : weaponShoot1;

            else if (currentWeaponState == WeaponState.Reloading)
                currentWeaponSprite = (currentWeaponSprite == weaponReload1) ? weaponReload2 : weaponReload1;

            // Update bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Update(levelManager.CurrentMap);
                if (!bullets[i].Alive) { bullets.RemoveAt(i); continue; }

                foreach (var enemy in enemyManager.Enemies)
                {
                    double dx = bullets[i].X - enemy.X;
                    double dy = bullets[i].Y - enemy.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < 0.5)
                    {
                        enemy.Health -= 50;
                        bullets[i].Alive = false;

                        if (enemy.Health <= 0)
                        {
                            var deathSound = new SoundPlayer(Properties.Resources.DeathSound);
                            deathSound.Play();

                            // Heal the player when an enemy dies
                            int healAmount = 20; // amount to heal
                            playerHealth += healAmount;
                            if (playerHealth > maxHealth) playerHealth = maxHealth; // cap at max health
                            // shows amount healed
                            playerHP.ShowHeal(healAmount);
                        }
                        break;
                    }
                }
            }

            // Update enemies
            enemyManager.UpdateEnemies(levelManager.CurrentMap, playerX, playerY);

            // Start level transition if all enemies are dead 
            if (!levelTransitioning && enemyManager.Enemies.Count == 0)
            {
                levelTransitioning = true;
                transitionTimer = 30; // timer for next level transition
            }

            // Handle level transition countdown
            if (levelTransitioning)
            {
                transitionTimer--;
                if (transitionTimer <= 0)
                {
                    levelManager.LoadNextLevel(enemyManager);

                    var startPos = levelManager.GetPlayerStartPosition();
                    playerX = startPos.x;
                    playerY = startPos.y;
                    playerAngle = startPos.angle;

                    bullets.Clear();
                    levelTransitioning = false;

                    // Optional: update background music based on level
                    string nextMusic = (levelManager.CurrentLevel > 3) ? music2 : music1;
                    if (currentMusic != nextMusic)
                    {
                        bgMusicPlayer.URL = nextMusic;
                        bgMusicPlayer.controls.play();
                        currentMusic = nextMusic;
                    }
                }
            }

            Invalidate();
        }

        // Simplified OnPaint - just calls the renderer
        protected override void OnPaint(PaintEventArgs e)
        {
            renderer.Render(
                e.Graphics,
                skybox,
                floorSprite,
                wallSprite,
                currentWeaponSprite,
                levelManager.CurrentMap,
                playerX,
                playerY,
                playerAngle,
                enemyManager
            );

            // Draw level info
            using (Font font = new Font("Arial", 16, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (SolidBrush shadowBrush = new SolidBrush(Color.Black))
            {
                string levelText = levelManager.GetLevelInfo();
                string enemiesText = $"Enemies: {enemyManager.Enemies.Count}";

                // Draw shadow
                e.Graphics.DrawString(levelText, font, shadowBrush, 11, 11);
                e.Graphics.DrawString(enemiesText, font, shadowBrush, 11, 36);

                // Draw text
                e.Graphics.DrawString(levelText, font, brush, 10, 10);
                e.Graphics.DrawString(enemiesText, font, brush, 10, 35);
            }

            // Draw player HP
            playerHP.Draw(e.Graphics, playerHealth, maxHealth, ScreenHeight);

            // Draw level complete message during transition
            if (levelTransitioning)
            {
                using (Font bigFont = new Font("Arial", 32, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(Color.Yellow))
                using (SolidBrush shadowBrush = new SolidBrush(Color.Black))
                {
                    string message = "LEVEL COMPLETE!";
                    SizeF textSize = e.Graphics.MeasureString(message, bigFont);
                    float x = (ScreenWidth - textSize.Width) / 2;
                    float y = (ScreenHeight - textSize.Height) / 2;

                    // Draw shadow
                    e.Graphics.DrawString(message, bigFont, shadowBrush, x + 2, y + 2);
                    // Draw text
                    e.Graphics.DrawString(message, bigFont, brush, x, y);
                }
            }
        }

        // Keyboard inputs
        private void KeyboardInputs(object sender, KeyEventArgs e)
        {
            // Don't allow movement during level transition
            if (levelTransitioning || isGameOver) return;

            double moveSpeed = 0.3, rotSpeed = 0.2;

            // Space bar shoots
            if (e.KeyCode == Keys.Space)
                SetWeaponState(WeaponState.Shooting);
            else
                SetWeaponState(WeaponState.Idle);

            // W - moves forward
            if (e.KeyCode == Keys.W)
            {
                double newX = playerX + Math.Cos(playerAngle) * moveSpeed;
                double newY = playerY + Math.Sin(playerAngle) * moveSpeed;
                if (levelManager.CurrentMap[(int)newY, (int)newX] == 0)
                {
                    playerX = newX;
                    playerY = newY;
                }
            }

            // S - moves backward
            if (e.KeyCode == Keys.S)
            {
                double newX = playerX - Math.Cos(playerAngle) * moveSpeed;
                double newY = playerY - Math.Sin(playerAngle) * moveSpeed;
                if (levelManager.CurrentMap[(int)newY, (int)newX] == 0)
                {
                    playerX = newX;
                    playerY = newY;
                }
            }

            // A/D - rotate
            if (e.KeyCode == Keys.A) playerAngle -= rotSpeed;
            if (e.KeyCode == Keys.D) playerAngle += rotSpeed;

            // R - restart game (for testing)
            if (e.KeyCode == Keys.R)
            {
                levelManager.Reset(enemyManager);
                var startPos = levelManager.GetPlayerStartPosition();
                playerX = startPos.x;
                playerY = startPos.y;
                playerAngle = startPos.angle;
                bullets.Clear();
            }
        }

        // Handles changing weapon states
        private void SetWeaponState(WeaponState state)
        {
            currentWeaponState = state;

            switch (state)
            {
                case WeaponState.Idle:
                    currentWeaponSprite = weaponIdle;
                    break;

                case WeaponState.Shooting:
                    currentWeaponSprite = weaponShoot1;
                    ShootSound.Play();
                    bullets.Add(new Bullet(playerX + Math.Cos(playerAngle) * 0.5,
                        playerY + Math.Sin(playerAngle) * 0.5, playerAngle));
                    StartTimer(150, () => SetWeaponState(WeaponState.Reloading));
                    break;

                case WeaponState.Reloading:
                    currentWeaponSprite = weaponReload1;
                    ReloadSound.Play();
                    StartTimer(400, () => { currentWeaponState = WeaponState.Idle; currentWeaponSprite = weaponIdle; });
                    break;
            }

            if (state != WeaponState.Idle)
            {
                var revertTimer = new Timer();
                revertTimer.Interval = (state == WeaponState.Shooting) ? 150 : 400;
                revertTimer.Tick += (s, e) =>
                {
                    currentWeaponState = WeaponState.Idle;
                    currentWeaponSprite = weaponIdle;
                    ((Timer)s).Stop();
                    ((Timer)s).Dispose();
                };
                revertTimer.Start();
            }
        }

        // when closing the form, stop all timers and music so that game does not continue running in background
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Stop the main game loop
            foreach (var t in this.Controls.OfType<Timer>())
            {
                t.Stop();
                t.Dispose();
            }

            // Stop background music
            if (bgMusicPlayer != null)
            {
                bgMusicPlayer.controls.stop();
                bgMusicPlayer.close();
                bgMusicPlayer = null;
            }

            // Mark game over 
            isGameOver = true;
        }

        private void GameOver()
        {
            bgMusicPlayer.controls.stop();
            MessageBox.Show("You have been defeated!", "Game Over");
            this.Close();
            MainMenu menu = new MainMenu();
            menu.Show();
        }

        // Helper method to run code after a delay
        private void StartTimer(int interval, Action action)
        {
            var timer = new Timer { Interval = interval };
            timer.Tick += (s, e) => { ((Timer)s).Stop(); ((Timer)s).Dispose(); action(); };
            timer.Start();
        }

        // Cleanup
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderer?.Dispose();
                wallSprite?.Dispose();
                floorSprite?.Dispose();
                skybox?.Dispose();

                if (bgMusicPlayer != null)
                {
                    bgMusicPlayer.controls.stop();
                    bgMusicPlayer.close();
                    bgMusicPlayer = null;
                }
            }
            base.Dispose(disposing);
        }

    }
}
