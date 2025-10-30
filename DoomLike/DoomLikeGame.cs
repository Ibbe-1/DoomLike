using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WMPLib;
using static DoomLike.Hitreg;
using Timer = System.Windows.Forms.Timer;

namespace DoomLike
{
    public class DoomLikeGame : Form
    {
        // we use this enum to track what the weapon is doing
        private enum WeaponState { Idle, Shooting, Reloading }

        // current state of the weapon with the help of enum.
        private WeaponState currentWeaponState = WeaponState.Idle;
        //wall and floor sprites
        private Bitmap wallSprite, floorSprite, skybox, buffer;
        // we embed our weapon sprites here
        private Bitmap weaponIdle, weaponShoot1, weaponShoot2, weaponReload1, weaponReload2, currentWeaponSprite;
        // we embed our sounds here
        private SoundPlayer ShootSound, ReloadSound, death;
        private WindowsMediaPlayer bgMusicPlayer;
        // enemy manager
        private EnemyManager enemyManager = new EnemyManager();
        // stores all active bullets in the game
        private List<Bullet> bullets = new List<Bullet>();

        // we generate our map here 
        private const int MapWidth = 8, MapHeight = 8, ScreenWidth = 640, ScreenHeight = 480;
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

        // player starts in the middle of the map at position 3.5, 3.5
        private double playerX = 3.5, playerY = 3.5, playerAngle = 0;

        public DoomLikeGame()
        {
            // displays text of the game
            Text = "Doomlike";
            // displays clientsize
            ClientSize = new Size(ScreenWidth, ScreenHeight);
            this.DoubleBuffered = true;
            // buffer is what we draw everything to before showing it on screen
            buffer = new Bitmap(ScreenWidth, ScreenHeight);

            //we load our wall and floor sprites here
            wallSprite = new Bitmap(Properties.Resources.WallTexture);
            floorSprite = new Bitmap(Properties.Resources.FloorTexture);
            skybox = new Bitmap(Properties.Resources.skybox);

            // we load our weapon sprites here
            weaponIdle = new Bitmap(Properties.Resources.WeaponIdle);
            weaponShoot1 = new Bitmap(Properties.Resources.shooting1);
            weaponShoot2 = new Bitmap(Properties.Resources.shooting2);
            weaponReload1 = new Bitmap(Properties.Resources.reload1);
            weaponReload2 = new Bitmap(Properties.Resources.reload2);
            // starts with the idle sprite showing
            currentWeaponSprite = weaponIdle;

            // we load our sounds here
            ShootSound = new SoundPlayer(Properties.Resources.ShootSound);
            ReloadSound = new SoundPlayer(Properties.Resources.ReloadSound);
            death = new SoundPlayer(Properties.Resources.DeathSound);

            // due to Windows Media Player needing a file path, we create a temporary file for the background music
            // we cant use the background music at the same time as shooting animation, so we have to use media player
            bgMusicPlayer = new WindowsMediaPlayer();
            string musicPath = Path.Combine(Application.StartupPath, "BGmusic.wav");
            // we write the embedded resource to a temporary file
            using (var rs = Properties.Resources.BGmusic)
            using (var fs = new FileStream(musicPath, FileMode.Create, FileAccess.Write))
                rs.CopyTo(fs);

            // Set the player URL to the temporary file
            bgMusicPlayer.URL = musicPath;
            bgMusicPlayer.settings.setMode("loop", true);
            bgMusicPlayer.controls.play();

            // Game loop timer - runs every 20ms which gives us around 50 FPS
            Timer timer = new Timer { Interval = 20 }; // per frame, adjust as needed
            timer.Tick += (s, e) =>
            {
                // Animate weapon frames - switches between frames to make it look like its moving
                if (currentWeaponState == WeaponState.Shooting)
                    currentWeaponSprite = (currentWeaponSprite == weaponShoot1) ? weaponShoot2 : weaponShoot1;
                else if (currentWeaponState == WeaponState.Reloading)
                    currentWeaponSprite = (currentWeaponSprite == weaponReload1) ? weaponReload2 : weaponReload1;

                // 🔫 Update bullets - we go backwards through the list so we can safely remove bullets
                for (int i = bullets.Count - 1; i >= 0; i--)
                {
                    // move the bullet forward
                    bullets[i].Update(map);
                    // if bullet hit a wall or went too far, remove it
                    if (!bullets[i].Alive) { bullets.RemoveAt(i); continue; }

                    // Check for enemy hit - see if bullet is close enough to any enemy
                    foreach (var enemy in enemyManager.Enemies)
                    {
                        // calculate distance between bullet and enemy
                        double dx = bullets[i].X - enemy.X, dy = bullets[i].Y - enemy.Y;
                        if (Math.Sqrt(dx * dx + dy * dy) < 0.5)
                        {
                            // bullet hit the enemy, do damage
                            enemy.Health -= 50;
                            bullets[i].Alive = false;
                            if (enemy.Health <= 0)
                            {
                                // Play the death sound - create new instance so it doesn't get cut off by reload sound
                                var deathSound = new SoundPlayer(Properties.Resources.DeathSound);
                                deathSound.Play();
                            }
                            break;
                        }
                    }
                }

                // update enemy positions and AI
                enemyManager.UpdateEnemies(map, playerX, playerY);
                // Redraw the screen
                Invalidate();
            };

            // spawn some enemies in the map for testing
            enemyManager.SpawnEnemy(2.5, 3.5);
            enemyManager.SpawnEnemy(3.5, 6.5);
            enemyManager.SpawnEnemy(4.5, 1.5);



            timer.Start();
            this.KeyDown += KeyboardInputs;
        }

        // this is where we do all our rendering
        protected override void OnPaint(PaintEventArgs e)
        {
            // lock the buffer so we can write pixels to it directly
            var bufferData = buffer.LockBits(new Rectangle(0, 0, buffer.Width, buffer.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = bufferData.Stride; // stride is how many bytes per row
            byte[] pixelBuffer = new byte[Math.Abs(stride) * buffer.Height];

            // Lock textures - we need to lock them to read their pixel data fast
            var (skyPixels, skyData, skyStride) = LockTexture(skybox);
            var (floorPixels, floorData, floorStride) = LockTexture(floorSprite);
            var (wallPixels, wallData, wallStride) = LockTexture(wallSprite);

            //Draw skybox (ceiling) - just tiles the skybox texture across the top half
            for (int y = 0; y < ScreenHeight / 2; y++)
                for (int x = 0; x < ScreenWidth; x++)
                    CopyPixel(pixelBuffer, y * stride + x * 4, skyPixels,
                        (y % skybox.Height) * skyStride + (x % skybox.Width) * 4);

            // Draw floor using floor texture - this uses raycasting to figure out what floor texture to show
            for (int y = ScreenHeight / 2; y < ScreenHeight; y++)
            {
                // calculate the ray directions for the left and right edges of the screen
                double angle0 = playerAngle - Math.PI / 4, angle1 = playerAngle + Math.PI / 4;
                double rayDirX0 = Math.Cos(angle0), rayDirY0 = Math.Sin(angle0);
                double rayDirX1 = Math.Cos(angle1), rayDirY1 = Math.Sin(angle1);
                // figure out how far away this row of floor is from the player
                double rowDistance = 0.5 * ScreenHeight / (y - ScreenHeight / 2);
                // calculate how much to step across the floor for each pixel
                double floorStepX = rowDistance * (rayDirX1 - rayDirX0) / ScreenWidth;
                double floorStepY = rowDistance * (rayDirY1 - rayDirY0) / ScreenWidth;
                // starting position for this row of floor
                double floorX = playerX + rowDistance * rayDirX0;
                double floorY = playerY + rowDistance * rayDirY0;

                for (int x = 0; x < ScreenWidth; x++)
                {
                    // get the texture coordinates - the & is to wrap them around if they go past the edge
                    int tx = (int)(floorSprite.Width * (floorX - Math.Floor(floorX))) & (floorSprite.Width - 1);
                    int ty = (int)(floorSprite.Height * (floorY - Math.Floor(floorY))) & (floorSprite.Height - 1);
                    // copy the pixel from the floor texture to the screen
                    CopyPixel(pixelBuffer, y * stride + x * 4, floorPixels, ty * floorStride + tx * 4);
                    // move to the next floor position
                    floorX += floorStepX;
                    floorY += floorStepY;
                }
            }

            // --- Draw walls - this is the main raycasting part for the walls
            for (int x = 0; x < ScreenWidth; x++)
            {
                // calculate the ray angle for this vertical strip of screen
                double rayAngle = playerAngle + Math.Atan(2 * x / (double)ScreenWidth - 1);
                double rayDirX = Math.Cos(rayAngle), rayDirY = Math.Sin(rayAngle);
                // start at the player position
                int mapX = (int)playerX, mapY = (int)playerY;
                // figure out how far the ray has to travel to cross one grid square
                double deltaDistX = Math.Abs(1 / rayDirX), deltaDistY = Math.Abs(1 / rayDirY);
                // which direction to step in (1 or -1)
                int stepX = rayDirX < 0 ? -1 : 1, stepY = rayDirY < 0 ? -1 : 1;
                // calculate distance to the next grid line in each direction
                double sideDistX = (rayDirX < 0 ? playerX - mapX : mapX + 1.0 - playerX) * deltaDistX;
                double sideDistY = (rayDirY < 0 ? playerY - mapY : mapY + 1.0 - playerY) * deltaDistY;
                // keeps track of if we hit a horizontal or vertical wall
                bool raySide = false;

                // DDA algorithm - step through the grid until we hit a wall
                while (true)
                {
                    // check which direction is closer and step that way
                    if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; raySide = false; }
                    else { sideDistY += deltaDistY; mapY += stepY; raySide = true; }
                    // if we hit a wall or went out of bounds, stop
                    if (mapX < 0 || mapX >= MapWidth || mapY < 0 || mapY >= MapHeight || map[mapY, mapX] > 0) break;
                }

                // calculate perpendicular distance to avoid fisheye effect
                double perpWallDist = raySide ? (sideDistY - deltaDistY) : (sideDistX - deltaDistX);
                // figure out how tall to draw the wall - closer walls are taller
                int lineHeight = (int)(ScreenHeight / perpWallDist);
                // calculate where to start and stop drawing the wall on screen
                int drawStart = Math.Max(0, -lineHeight / 2 + ScreenHeight / 2);
                int drawEnd = Math.Min(ScreenHeight - 1, lineHeight / 2 + ScreenHeight / 2);
                // figure out where we hit the wall so we know which part of the texture to use
                double wallX = (raySide ? playerX + perpWallDist * rayDirX : playerY + perpWallDist * rayDirY) -
                    Math.Floor(raySide ? playerX + perpWallDist * rayDirX : playerY + perpWallDist * rayDirY);
                // convert to texture x coordinate
                int texX = (int)(wallX * wallSprite.Width);
                // flip the texture for certain wall sides so it looks right
                if ((!raySide && rayDirX > 0) || (raySide && rayDirY < 0)) texX = wallSprite.Width - texX - 1;

                // draw each pixel of the wall column
                for (int y = drawStart; y < drawEnd; y++)
                {
                    // figure out which y coordinate of the texture to use
                    int texY = (((y * 256 - ScreenHeight * 128 + lineHeight * 128) * wallSprite.Height) / lineHeight) / 256;
                    // make sure we dont go out of bounds on the texture
                    texY = Math.Max(0, Math.Min(texY, wallSprite.Height - 1));
                    texX = Math.Max(0, Math.Min(texX, wallSprite.Width - 1));
                    // copy the pixel from wall texture to screen
                    CopyPixel(pixelBuffer, y * stride + x * 4, wallPixels, texY * wallStride + texX * 4);
                }
            }

            // Copy back to buffer and unlock - writes our pixel data to the actual bitmap
            Marshal.Copy(pixelBuffer, 0, bufferData.Scan0, pixelBuffer.Length);
            buffer.UnlockBits(bufferData);
            skybox.UnlockBits(skyData);
            floorSprite.UnlockBits(floorData);
            wallSprite.UnlockBits(wallData);

            // Draw enemies - enemies are drawn as sprites on top of the 3D world
            using (Graphics g = Graphics.FromImage(buffer))
                enemyManager.DrawEnemies(g, playerX, playerY, playerAngle, ScreenWidth, ScreenHeight, map);

            // Render buffer to screen - finally show everything we drew
            e.Graphics.DrawImage(buffer, 0, 0);
            // Draw weapon - weapon is drawn on top of everything else so it looks like you're holding it
            e.Graphics.DrawImage(currentWeaponSprite, (ScreenWidth - 220) / 2, ScreenHeight - 200, 220, 200);
        }

        // helper method to lock a texture and get its pixel data
        private (byte[], BitmapData, int) LockTexture(Bitmap texture)
        {
            var data = texture.LockBits(new Rectangle(0, 0, texture.Width, texture.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[Math.Abs(data.Stride) * texture.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return (pixels, data, data.Stride);
        }

        // helper to copy a pixel from one array to another - copies BGRA values
        private void CopyPixel(byte[] dest, int dIdx, byte[] src, int sIdx)
        {
            dest[dIdx] = src[sIdx];         // B
            dest[dIdx + 1] = src[sIdx + 1]; // G
            dest[dIdx + 2] = src[sIdx + 2]; // R
            dest[dIdx + 3] = src[sIdx + 3]; // A
        }

        // for our WASD
        private void KeyboardInputs(object sender, KeyEventArgs e)
        {
            double moveSpeed = 0.3, rotSpeed = 0.2;

            // space bar shoots the gun
            if (e.KeyCode == Keys.Space) // shoot
                SetWeaponState(WeaponState.Shooting);
            else
                SetWeaponState(WeaponState.Idle);

            // for when the player uses W - moves forward
            if (e.KeyCode == Keys.W)
            {
                // calculate where the player would move to
                double newX = playerX + Math.Cos(playerAngle) * moveSpeed;
                double newY = playerY + Math.Sin(playerAngle) * moveSpeed;
                // this makes sure that we don't collide with the walls. The walls are represented by 1 and the available space is 0.
                if (map[(int)newY, (int)newX] == 0) { playerX = newX; playerY = newY; }
            }
            // For when the player uses S - moves backward
            if (e.KeyCode == Keys.S)
            {
                double newX = playerX - Math.Cos(playerAngle) * moveSpeed;
                double newY = playerY - Math.Sin(playerAngle) * moveSpeed;
                // check if the new position is empty before moving
                if (map[(int)newY, (int)newX] == 0) { playerX = newX; playerY = newY; }
            }
            // for rotating - A rotates left, D rotates right
            if (e.KeyCode == Keys.A) playerAngle -= rotSpeed;
            if (e.KeyCode == Keys.D) playerAngle += rotSpeed;
        }

        // handles changing weapon states and triggering animations/sounds
        private void SetWeaponState(WeaponState state)
        {
            currentWeaponState = state;

            switch (state)
            {
                case WeaponState.Idle:
                    // just show the idle sprite when not doing anything
                    currentWeaponSprite = weaponIdle;
                    break;
                case WeaponState.Shooting:
                    // switch to shooting sprite and play sound
                    currentWeaponSprite = weaponShoot1;
                    ShootSound.Play();
                    // spawn a bullet slightly in front of the player so it doesnt hit them
                    bullets.Add(new Bullet(playerX + Math.Cos(playerAngle) * 0.5,
                        playerY + Math.Sin(playerAngle) * 0.5, playerAngle));

                    // Automatically start reload after shooting - wait 150ms then reload
                    StartTimer(150, () => SetWeaponState(WeaponState.Reloading));
                    break;

                case WeaponState.Reloading:
                    // switch to reload sprite and play reload sound
                    currentWeaponSprite = weaponReload1;
                    ReloadSound.Play();

                    // End reload and go to idle - wait 400ms then go back to idle
                    StartTimer(400, () => { currentWeaponState = WeaponState.Idle; currentWeaponSprite = weaponIdle; });
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

        // helper method to run code after a delay using a timer
        private void StartTimer(int interval, Action action)
        {
            var timer = new Timer { Interval = interval };
            timer.Tick += (s, e) => { ((Timer)s).Stop(); ((Timer)s).Dispose(); action(); };
            timer.Start();
        }
    }
}