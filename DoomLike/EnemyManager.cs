using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;

namespace DoomLike
{
    public class EnemyManager
    {
        private enum NPCstate
        {
            Idle,
            Attacking,
            Death
        }

        private Bitmap EnemyMove1;      // Enemy walking animation frame 1 (facing forward)
        private Bitmap EnemyMove2;      // Enemy walking animation frame 2 (facing forward)
        private Bitmap EnemyReady;      // Enemy ready/idle pose, not moving
        private Bitmap EnemyShooting;   // Enemy shooting animation
        private Bitmap EnemyDeath1;     // Enemy death animation frame 1
        private Bitmap EnemyDeath2;     // Enemy death animation frame 2
        private Bitmap backmoving1;     // Enemy walking backwards frame 1 (away from player)
        private Bitmap backmoving2;     // Enemy walking backwards frame 2
        private Bitmap sideleft1;       // Enemy walking left side frame 1
        private Bitmap sidemidwalk;     // Enemy walking left side frame 2 (middle frame)
        private Bitmap sideleft2;       // Enemy walking left side frame 3

        public EnemyManager()
        {
            // Load forward walking frames
            EnemyMove1 = new Bitmap(Properties.Resources.EnemyMove1);
            EnemyMove2 = new Bitmap(Properties.Resources.EnemyMove2);
            // Load idle / ready pose
            EnemyReady = new Bitmap(Properties.Resources.EnemyReady);
            EnemyShooting = new Bitmap(Properties.Resources.EnemyShooting);
            // Load death animation frames
            EnemyDeath1 = new Bitmap(Properties.Resources.EnemyDeath1);
            EnemyDeath2 = new Bitmap(Properties.Resources.EnemyDeath2);
            // Load backward walking frames
            backmoving1 = new Bitmap(Properties.Resources.backmoving1);
            backmoving2 = new Bitmap(Properties.Resources.backmoving2);
            // Load left side walking frames
            sideleft1 = new Bitmap(Properties.Resources.sideleft1);
            sidemidwalk = new Bitmap(Properties.Resources.sidemidwalk);
            sideleft2 = new Bitmap(Properties.Resources.sideleft2);
        }

        private NPCstate NPCidling = NPCstate.Idle;

        private List<NPCEnemy> enemies = new List<NPCEnemy>();

        public class NPCEnemy
        {
            public double X, Y;
            public double Direction;
            public double Speed = 0.03;
            public int Health = 100;
            public bool Alive => Health > 0;
            public bool ToggleFrame = false; // alternates between frames for a slight animation effect
            public bool DeathAnimationStarted = false; // flag to track if death animation has started
            public int DeathFrame = 0; // current frame of death animation

            // shooting stuff
            public bool IsShooting = false; // is the enemy currently shooting
            public int ShootCooldown = 0; // cooldown between shots
            public bool CanSeePlayer = false; // can the enemy see the player right now
            public int ShootingAnimationFrames = 0; // how many frames left to show shooting animation
            public double LastMoveAngle = 0; // track which direction enemy is moving for sprite selection

            public NPCEnemy(double x, double y) { X = x; Y = y; }
        }

        public void SpawnEnemy(double x, double y)
        {
            enemies.Add(new NPCEnemy(x, y));
        }

        private bool IsVisible(double startX, double startY, double endX, double endY, int[,] map)
        {
            double dx = endX - startX;
            double dy = endY - startY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double stepX = dx / distance * 0.05;
            double stepY = dy / distance * 0.05;

            double x = startX;
            double y = startY;

            for (double i = 0; i < distance; i += 0.05)
            {
                int mapX = (int)x;
                int mapY = (int)y;

                if (mapX < 0 || mapX >= map.GetLength(1) || mapY < 0 || mapY >= map.GetLength(0))
                    return false;

                if (map[mapY, mapX] > 0) // wall
                    return false;

                x += stepX;
                y += stepY;
            }

            return true;
        }
        private (Bitmap, bool) GetDirectionalSprite(NPCEnemy enemy, double playerAngle)
        {
            // Compute relative movement angle of enemy vs player facing direction
            double relativeAngle = enemy.LastMoveAngle - playerAngle;
            while (relativeAngle > Math.PI) relativeAngle -= 2 * Math.PI;
            while (relativeAngle < -Math.PI) relativeAngle += 2 * Math.PI;

            double deg = relativeAngle * (180 / Math.PI); // convert to degrees
            bool flip = false; // whether sprite needs to be mirrored horizontally
            Bitmap sprite;

            // Select sprite based on the enemy's angle relative to the player
            if (deg >= -22.5 && deg < 22.5) // Front view
                sprite = enemy.ToggleFrame ? EnemyMove1 : EnemyMove2;
            else if (deg >= 22.5 && deg < 67.5) // Front-right
            {
                sprite = sideleft1;
                flip = true; // flip horizontally
            }
            else if (deg >= 67.5 && deg < 112.5) // Right side
            {
                sprite = enemy.ToggleFrame ? sidemidwalk : sideleft2;
                flip = true;
            }
            else if (deg >= 112.5 && deg < 157.5) // Back-right
            {
                sprite = backmoving1;
                flip = true;
            }
            else if (deg >= 157.5 || deg < -157.5) // Back view
                sprite = enemy.ToggleFrame ? backmoving1 : backmoving2;
            else if (deg >= -157.5 && deg < -112.5) // Back-left
                sprite = backmoving1;
            else if (deg >= -112.5 && deg < -67.5) // Left side
                sprite = enemy.ToggleFrame ? sidemidwalk : sideleft1;
            else // Front-left
                sprite = sideleft1;

            return (sprite, flip);
        }

        public void DrawEnemies(Graphics g, double playerX, double playerY, double playerAngle,
                         int screenWidth, int screenHeight, int[,] map)
        {
            int fixedSize = 255; // Base size of enemy sprites

            foreach (var enemy in enemies)
            {
                // Compute relative position to player
                double dx = enemy.X - playerX;
                double dy = enemy.Y - playerY;
                double angleToPlayer = Math.Atan2(dy, dx) - playerAngle;

                // Normalize angle to [-π, π] range
                while (angleToPlayer > Math.PI) angleToPlayer -= 2 * Math.PI;
                while (angleToPlayer < -Math.PI) angleToPlayer += 2 * Math.PI;

                // Skip if enemy is outside player's field of view (FOV)
                if (Math.Abs(angleToPlayer) > Math.PI / 4) continue;

                // Skip if enemy is blocked by walls
                if (!IsVisible(playerX, playerY, enemy.X, enemy.Y, map)) continue;

                // Screen x-coordinate for the enemy sprite
                int x = (int)((screenWidth / 2) * (1 + Math.Tan(angleToPlayer)));
                int y = screenHeight - fixedSize; // Draw from bottom of screen upward

                Bitmap sprite;
                bool flip = false;

                if (enemy.Alive)
                {
                    // If enemy is shooting, use shooting animation
                    if (enemy.ShootingAnimationFrames > 0)
                    {
                        sprite = (enemy.ShootingAnimationFrames % 4 < 2) ? EnemyShooting : EnemyReady;
                    }
                    else
                    {
                        // Otherwise, select sprite based on movement direction
                        (sprite, flip) = GetDirectionalSprite(enemy, playerAngle);
                    }
                }
                else
                {
                    // Enemy is dead, show death sprite
                    sprite = EnemyDeath2;
                }

                // Calculate top-left corner to draw sprite
                int drawX = x - fixedSize / 2;
                int drawY = y;

                // Flip sprite horizontally if needed
                if (flip)
                {
                    g.TranslateTransform(drawX + fixedSize, drawY);
                    g.ScaleTransform(-1, 1);
                    g.DrawImage(sprite, 0, 0, fixedSize, fixedSize);
                    g.ResetTransform();
                }
                else
                {
                    g.DrawImage(sprite, drawX, drawY, fixedSize, fixedSize);
                }



            }
        }

        public void UpdateEnemies(int[,] map, double playerX, double playerY)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive)
                {
                    // Start death animation if not already started
                    if (!enemy.DeathAnimationStarted)
                    {
                        enemy.DeathAnimationStarted = true;
                        enemy.DeathFrame = 0;
                    }
                    continue; // dead enemies don't move
                }

                // check if enemy can see player
                enemy.CanSeePlayer = IsVisible(enemy.X, enemy.Y, playerX, playerY, map);

                // calculate distance to player
                double dx = playerX - enemy.X;
                double dy = playerY - enemy.Y;
                double distanceToPlayer = Math.Sqrt(dx * dx + dy * dy);

                // countdown shooting animation frames
                if (enemy.ShootingAnimationFrames > 0)
                {
                    enemy.ShootingAnimationFrames--;
                }

                // if player is too close (less than 3 units), back away and show movement
                if (enemy.CanSeePlayer && distanceToPlayer < 3.0)
                {
                    // calculate direction AWAY from player
                    double angleAwayFromPlayer = Math.Atan2(-dy, -dx);
                    enemy.LastMoveAngle = angleAwayFromPlayer; // track movement direction for sprite
                    
                    // try to move away from player
                    double newX = enemy.X + Math.Cos(angleAwayFromPlayer) * enemy.Speed;
                    double newY = enemy.Y + Math.Sin(angleAwayFromPlayer) * enemy.Speed;

                    if (map[(int)newY, (int)newX] == 0)
                    {
                        enemy.X = newX;
                        enemy.Y = newY;
                        enemy.ToggleFrame = !enemy.ToggleFrame; // walk animation while backing up
                    }
                    else
                    {
                        // if can't back up, just turn randomly
                        enemy.Direction = new Random().NextDouble() * Math.PI * 2;
                    }
                    
                    // reset shooting while backing up
                    enemy.ShootingAnimationFrames = 0;
                }
                // if enemy can see player and is at good distance (3-8 units), stand and shoot
                else if (enemy.CanSeePlayer && distanceToPlayer >= 3.0 && distanceToPlayer < 8.0)
                {
                    // enemy stands still and shoots
                    if (enemy.ShootCooldown <= 0)
                    {
                        // start shooting - trigger shooting animation
                        enemy.IsShooting = true;
                        enemy.ShootingAnimationFrames = 15; // show shooting sprites longer since enemy is standing still
                        enemy.ShootCooldown = 60; // wait 60 frames before next shot (about 1.2 seconds at 50fps)

                        // play enemy shooting sound - create new instance so sounds don't overlap weirdly
                        var enemyShootSound = new SoundPlayer(Properties.Resources.EnemyShootSound);
                        enemyShootSound.Play();
                    }
                    else
                    {
                        // cooldown between shots - enemy stands still
                        enemy.ShootCooldown--;
                    }
                }
                else
                {
                    // enemy can't see player or is too far, so wander around
                    enemy.IsShooting = false;
                    enemy.ShootingAnimationFrames = 0;
                    if (enemy.ShootCooldown > 0) enemy.ShootCooldown--;
                    
                    // normal wandering behavior
                    enemy.LastMoveAngle = enemy.Direction; // track movement direction for sprite
                    double newX = enemy.X + Math.Cos(enemy.Direction) * enemy.Speed;
                    double newY = enemy.Y + Math.Sin(enemy.Direction) * enemy.Speed;

                    // check X movement
                    if (map[(int)enemy.Y, (int)newX] == 0)
                        enemy.X = newX;

                    // check Y movement
                    if (map[(int)newY, (int)enemy.X] == 0)
                        enemy.Y = newY;

                    // toggle walk frame only if moved
                    if (enemy.X != newX || enemy.Y != newY)
                        enemy.ToggleFrame = !enemy.ToggleFrame;
                    else
                        enemy.Direction = new Random().NextDouble() * Math.PI * 2; // pick a new random direction if blocked
                }
            }
        }

        public List<NPCEnemy> Enemies => enemies;
    }
}