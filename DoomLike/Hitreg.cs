using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLike
{
    internal class Hitreg
    {
        public class Bullet
        {
            public double X, Y;
            public double Direction;
            public double Speed = 0.5;
            public bool Alive = true;

            public Bullet(double x, double y, double direction)
            {
                X = x;
                Y = y;
                Direction = direction;
            }

            public void Update(int[,] map)
            {
                double newX = X + Math.Cos(Direction) * Speed;
                double newY = Y + Math.Sin(Direction) * Speed;

                // If bullet hits wall, it dies
                if (map[(int)newY, (int)newX] == 0)
                {
                    X = newX;
                    Y = newY;
                }
                else
                {
                    Alive = false;
                }
            }
        }
    }
}
