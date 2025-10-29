using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DoomLike
{
    public class DoomLikeGame : Form
    {
        
        public DoomLikeGame()
        {
            // displays text of the game
            this.Text = "Doomlike";
            // displays clientsize
            this.ClientSize = new Size(ScreenWidth, ScreenHeight);
            this.DoubleBuffered = true;
            buffer = new Bitmap(ScreenWidth, ScreenHeight);

            Timer timer = new Timer { Interval = 16 };
            timer.Tick += (s, e) => Invalidate();
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
                }
            }
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

        // for our WASD
        private void KeyboardInputs(object sender, KeyEventArgs e)
        {
            double moveSpeed = 0.1;
            double rotationSpeed = 0.1;

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

    }
}