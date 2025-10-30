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

            // Setup background music
            bgMusicPlayer = new WindowsMediaPlayer();
            string musicPath = Path.Combine(Application.StartupPath, "BGmusic.wav");
            using (var rs = Properties.Resources.BGmusic)
            using (var fs = new FileStream(musicPath, FileMode.Create, FileAccess.Write))
                rs.CopyTo(fs);

            bgMusicPlayer.URL = musicPath;
            bgMusicPlayer.settings.setMode("loop", true);
            bgMusicPlayer.controls.play();

            // Spawn initial enemies for level 1
            levelManager.Reset(enemyManager);

            // Game loop timer
            Timer timer = new Timer { Interval = 20 };
            timer.Tick += (s, e) => GameLoop();

            timer.Start();
            this.KeyDown += KeyboardInputs;
        }

        private void GameLoop()
        {
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

                // Check for enemy hit
                foreach (var enemy in enemyManager.Enemies)
                {
                    double dx = bullets[i].X - enemy.X, dy = bullets[i].Y - enemy.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < 0.5)
                    {
                        enemy.Health -= 50;
                        bullets[i].Alive = false;
                        if (enemy.Health <= 0)
                        {
                            var deathSound = new SoundPlayer(Properties.Resources.DeathSound);
                            deathSound.Play();
                        }
                        break;
                    }
                }
            }

            // Update enemy positions and AI
            enemyManager.UpdateEnemies(levelManager.CurrentMap, playerX, playerY);

            // Check for level completion
            if (levelManager.ShouldLoadNextLevel(enemyManager) && !levelTransitioning)
            {
                levelTransitioning = true;
                transitionTimer = 60; // Wait 60 frames (about 1.2 seconds) before loading next level
            }

            // Handle level transition
            if (levelTransitioning)
            {
                transitionTimer--;
                if (transitionTimer <= 0)
                {
                    // Load next level
                    levelManager.LoadNextLevel(enemyManager);

                    // Reset player position
                    var startPos = levelManager.GetPlayerStartPosition();
                    playerX = startPos.x;
                    playerY = startPos.y;
                    playerAngle = startPos.angle;

                    // Clear bullets
                    bullets.Clear();

                    levelTransitioning = false;
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
            if (levelTransitioning) return;

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
            }
            base.Dispose(disposing);
        }
    }
}