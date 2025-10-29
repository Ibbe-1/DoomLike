using System;
using System.Drawing;
using System.Security.Policy;
using System.Windows.Forms;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using System.Media;
using WMPLib;
using System.IO;

namespace DoomLike
{
    public class DoomLikeGame : Form
    {
        private enum WeaponState
        {
          Idle,
          Shooting, 
          Reloading
        }
        // current state of the weapon with the help of enum.
        private WeaponState currentWeaponState = WeaponState.Idle;
        // we embed our weapon sprites here
        private Bitmap weaponIdle;
        private Bitmap weaponShoot1;
        private Bitmap weaponShoot2;
        private Bitmap weaponReload1;
        private Bitmap weaponReload2;
        private Bitmap currentWeaponSprite;
        // we embed our sounds here
        private SoundPlayer ShootSound;
        private SoundPlayer ReloadSound;
        private WindowsMediaPlayer bgMusicPlayer;

        public DoomLikeGame()
        {
            // displays text of the game
            this.Text = "Doomlike";
            // displays clientsize
            this.ClientSize = new Size(ScreenWidth, ScreenHeight);
            this.DoubleBuffered = true;
            buffer = new Bitmap(ScreenWidth, ScreenHeight);
            // we load our weapon sprites here
            weaponIdle = new Bitmap(Properties.Resources.WeaponIdle);
            currentWeaponSprite = weaponIdle;
            weaponShoot1 = new Bitmap(Properties.Resources.shooting1);
            weaponShoot2 = new Bitmap(Properties.Resources.shooting2);
            weaponReload1 = new Bitmap(Properties.Resources.reload1);
            weaponReload2 = new Bitmap(Properties.Resources.reload2);
            // we load our sounds here
            ShootSound = new SoundPlayer(Properties.Resources.ShootSound);   
            ReloadSound = new SoundPlayer(Properties.Resources.ReloadSound);

            bgMusicPlayer = new WindowsMediaPlayer();

            // due to Windows Media Player needing a file path, we create a temporary file for the background music
            // we cant use the background music at the same time as shooting animation, so we have to use media player
            string musicPath = Path.Combine(Application.StartupPath, "BGmusic.wav");

            // we write the embedded resource to a temporary file
            using (var resourceStream = Properties.Resources.BGmusic)
            using (var fileStream = new FileStream(musicPath, FileMode.Create, FileAccess.Write))
            {
                resourceStream.CopyTo(fileStream);
            }

            // Set the player URL to the temporary file
            bgMusicPlayer.URL = musicPath;
            bgMusicPlayer.settings.setMode("loop", true);
            bgMusicPlayer.controls.play();

            Timer timer = new Timer { Interval = 100 }; // 100 ms per frame, adjust as needed
            timer.Tick += (s, e) =>
            {
                // Animate weapon frames
                if (currentWeaponState == WeaponState.Shooting)
                {
                    currentWeaponSprite = (currentWeaponSprite == weaponShoot1) ? weaponShoot2 : weaponShoot1;
                }
                else if (currentWeaponState == WeaponState.Reloading)
                {
                    currentWeaponSprite = (currentWeaponSprite == weaponReload1) ? weaponReload2 : weaponReload1;
                }

                // Redraw the screen
                Invalidate();
            };
            timer.Start();

            this.KeyDown += KeyboardInputs;
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            using (Graphics g = Graphics.FromImage(buffer))
            {
                g.Clear(Color.Gray);
                // we fill the ceiling and floor
                g.FillRectangle(Brushes.DarkRed, 0, 0, ScreenWidth, ScreenHeight);
                g.FillRectangle(Brushes.DimGray, 0, ScreenHeight / 2, ScreenWidth, ScreenHeight / 2);

                for (int x = 0; x < ScreenWidth; x++)
                {
                    // calculating the rays for the game
                    double cameraX = 2 * x / (double)ScreenWidth - 1;
                    double rayAngle = playerAngle + Math.Atan(cameraX);

                    double rayDirX = Math.Cos(rayAngle);
                    double rayDirY = Math.Sin(rayAngle);

                    int mapX = (int)playerX;
                    int mapY = (int)playerY;

                    double deltaDistX = Math.Abs(1 / rayDirX);
                    double deltaDistY = Math.Abs(1 / rayDirY);
                    // steps for the rays
                    int stepX = rayDirX < 0 ? -1 : 1;
                    int stepY = rayDirY < 0 ? -1 : 1;

                    double sideDistX = (rayDirX < 0 ? playerX - mapX : mapX + 1.0 - playerX) * deltaDistX;
                    double sideDistY = (rayDirY < 0 ? playerY - mapY : mapY + 1.0 - playerY) * deltaDistY;
                    // checks for if the ray hits a wall.
                    bool rayHit = false;
                    bool raySide = false;

                    while (!rayHit)
                    {
                        if (sideDistX < sideDistY)
                        {
                            sideDistX += deltaDistX;
                            mapX += stepX;
                            raySide = false;
                        }
                        else
                        {
                            sideDistY += deltaDistY;
                            mapY += stepY;
                            raySide = true;
                        }

                        if (mapX < 0 || mapX >= MapWidth || mapY < 0 || mapY >= MapHeight || map[mapY, mapX] > 0)
                            rayHit = true;
                    } // 

                    // we calculate the wall distance and height
                    double perpWallDist = raySide ? (sideDistY - deltaDistY) : (sideDistX - deltaDistX);
                    int lineHeight = (int)(ScreenHeight / perpWallDist);

                    int drawStart = Math.Max(0, -lineHeight / 2 + ScreenHeight / 2);
                    int drawEnd = Math.Min(ScreenHeight - 1, lineHeight / 2 + ScreenHeight / 2);

                    Color wallColor = raySide ? Color.FromArgb(100, 100, 100) : Color.FromArgb(180, 180, 180);

                    using (Pen pen = new Pen(wallColor))
                    {
                        g.DrawLine(pen, x, drawStart, x, drawEnd);

                    }
                }
            }
            // finally we draw the buffer to the screen
            e.Graphics.DrawImage(buffer, 0, 0);

            // we draw the weapon sprites here and set a certain size so there is no inconsistency
            int targetWidth = 220;
            int targetHeight = 200;

            // center horizontally and place near bottom
            int weaponX = (ScreenWidth - targetWidth) / 2;
            int weaponY = ScreenHeight - targetHeight;
            e.Graphics.DrawImage(currentWeaponSprite, weaponX, weaponY, targetWidth, targetHeight);
        }
        

        // we generate our map here 
        private const int MapWidth = 8;
        private const int MapHeight = 8;
        private const int ScreenWidth = 640;
        private const int ScreenHeight = 480;

        private int[,] map = new int[,]
        {
            // the 1 is wall, 0 is moveable space.
            {1,1,1,1,1,1,1,1},
            {1,0,0,0,0,0,0,1},
            {1,0,1,0,0,1,0,1},
            {1,0,0,0,0,0,0,1},
            {1,0,1,0,0,1,0,1},
            {1,0,0,0,0,0,0,1},
            {1,0,0,0,0,0,0,1},
            {1,1,1,1,1,1,1,1}
        };

        private double playerX = 3.5;
        private double playerY = 3.5;
        private double playerAngle = 0;
        private Bitmap buffer;
        private Bitmap weaponSprite;

        // for our WASD
        private void KeyboardInputs(object sender, KeyEventArgs e)
        {
            double moveSpeed = 0.1;
            double rotationSpeed = 0.1;


            if (e.KeyCode == Keys.Space) // shoot
                SetWeaponState(WeaponState.Shooting);
            else if (e.KeyCode == Keys.R) // reload
                SetWeaponState(WeaponState.Reloading);
            else
                SetWeaponState(WeaponState.Idle);

            // for when the player uses W
            if (e.KeyCode == Keys.W)
            {
                double PlayerMoveX = playerX + Math.Cos(playerAngle) * moveSpeed;
                double PlayerMoveY = playerY + Math.Sin(playerAngle) * moveSpeed;
                // this makes sure that we don't collide with the walls. The walls are represented by 1 and the available space is 0.
                if (map[(int)PlayerMoveY, (int)PlayerMoveX] == 0)
                {
                    playerX = PlayerMoveX;
                    playerY = PlayerMoveY;
                }
            }
            // For when the player uses S
            if (e.KeyCode == Keys.S)
            {
                double PlayerMoveX = playerX - Math.Cos(playerAngle) * moveSpeed;
                double PlayerMoveY = playerY - Math.Sin(playerAngle) * moveSpeed;

                if (map[(int)PlayerMoveY, (int)PlayerMoveX] == 0)
                {
                    playerX = PlayerMoveX;
                    playerY = PlayerMoveY;
                }
            }
            // for rotating
            if (e.KeyCode == Keys.A) playerAngle -= rotationSpeed;
            if (e.KeyCode == Keys.D) playerAngle += rotationSpeed;
        }
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
                    break;
                case WeaponState.Reloading:
                    currentWeaponSprite = weaponReload1;
                    ReloadSound.Play();
                    break;
            }

            // We use this to end the gun looping or reloading endlessly by making it revert after shooting or reloading to idle.
            if (state != WeaponState.Idle)
            {
                var revertTimer = new Timer();
                revertTimer.Interval = (state == WeaponState.Shooting) ? 150 : 400; // shooting faster, reload slower
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
    }
}