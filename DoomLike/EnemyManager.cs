using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;

namespace DoomLike
{
    public class EnemyManager
    {
        // Enemy states
        private enum NPCstate
        {
            Idle,
            Attacking,
            Death
        }
        // Event for when player is hit
        public Action<int>? OnPlayerHit; 

        // Enemy sprites
        private Bitmap EnemyMove1;
        private Bitmap EnemyMove2;
        private Bitmap EnemyReady;
        private Bitmap EnemyShooting;
        private Bitmap EnemyDeath1;
        private Bitmap EnemyDeath2;
        private Bitmap backmoving1;
        private Bitmap backmoving2;
        private Bitmap sideleft1;
        private Bitmap sidemidwalk;
        private Bitmap sideleft2;

        public EnemyManager()
        {
            // Load enemy images from resources
            EnemyMove1 = new Bitmap(Properties.Resources.EnemyMove1);
            EnemyMove2 = new Bitmap(Properties.Resources.EnemyMove2);
            EnemyReady = new Bitmap(Properties.Resources.EnemyReady);
            EnemyShooting = new Bitmap(Properties.Resources.EnemyShooting);
            EnemyDeath1 = new Bitmap(Properties.Resources.EnemyDeath1);
            EnemyDeath2 = new Bitmap(Properties.Resources.EnemyDeath2);
            backmoving1 = new Bitmap(Properties.Resources.backmoving1);
            backmoving2 = new Bitmap(Properties.Resources.backmoving2);
            sideleft1 = new Bitmap(Properties.Resources.sideleft1);
            sidemidwalk = new Bitmap(Properties.Resources.sidemidwalk);
            sideleft2 = new Bitmap(Properties.Resources.sideleft2);
        }

        private NPCstate NPCidling = NPCstate.Idle;
        private List<NPCEnemy> enemies = new List<NPCEnemy>();

        // Enemy class that holds info about each enemy
        public class NPCEnemy
        {
            public double X, Y;
            public double Direction;
            public double Speed = 0.03;
            public int Health = 100;
            public bool Alive => Health > 0;
            public bool ToggleFrame = false;
            public bool DeathAnimationStarted = false;
            public int DeathFrame = 0;
            public bool IsShooting = false;
            public int ShootCooldown = 0;
            public bool CanSeePlayer = false;
            public int ShootingAnimationFrames = 0;
            public double LastMoveAngle = 0;
            // this is so that the enemy death sprite spawns but then disappears after a short while so that the game can continue.
            public int DeathTimer = 0;

            public NPCEnemy(double x, double y) { X = x; Y = y; }
        }

        // Make a new enemy
        public void SpawnEnemy(double x, double y)
        {
            enemies.Add(new NPCEnemy(x, y));
        }

        // Clear all enemies from the list
        public void ClearEnemies()
        {
            enemies.Clear();
        }

        // Check if enemy can "see" the player (no wall in between)
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

                // Out of bounds
                if (mapX < 0 || mapX >= map.GetLength(1) || mapY < 0 || mapY >= map.GetLength(0))
                    return false;

                // Hit a wall
                if (map[mapY, mapX] > 0)
                    return false;

                x += stepX;
                y += stepY;
            }

            return true;
        }

        // Pick sprite depending on where enemy is facing
        private (Bitmap, bool) GetDirectionalSprite(NPCEnemy enemy, double playerAngle)
        {
            double relativeAngle = enemy.LastMoveAngle - playerAngle;
            while (relativeAngle > Math.PI) relativeAngle -= 2 * Math.PI;
            while (relativeAngle < -Math.PI) relativeAngle += 2 * Math.PI;

            double deg = relativeAngle * (180 / Math.PI);
            bool flip = false;
            Bitmap sprite;

            // Just choosing which image to show depending on angle
            if (deg >= -22.5 && deg < 22.5)
                sprite = enemy.ToggleFrame ? EnemyMove1 : EnemyMove2;
            else if (deg >= 22.5 && deg < 67.5)
            {
                sprite = sideleft1;
                flip = true;
            }
            else if (deg >= 67.5 && deg < 112.5)
            {
                sprite = enemy.ToggleFrame ? sidemidwalk : sideleft2;
                flip = true;
            }
            else if (deg >= 112.5 && deg < 157.5)
            {
                sprite = backmoving1;
                flip = true;
            }
            else if (deg >= 157.5 || deg < -157.5)
                sprite = enemy.ToggleFrame ? backmoving1 : backmoving2;
            else if (deg >= -157.5 && deg < -112.5)
                sprite = backmoving1;
            else if (deg >= -112.5 && deg < -67.5)
                sprite = enemy.ToggleFrame ? sidemidwalk : sideleft1;
            else
                sprite = sideleft1;

            return (sprite, flip);
        }

        // Draw enemies on the screen
        public void DrawEnemies(Graphics g, double playerX, double playerY, double playerAngle,
                         int screenWidth, int screenHeight, int[,] map)
        {
            int fixedSize = 255;

            foreach (var enemy in enemies)
            {
                // Figure out where enemy is compared to player
                double dx = enemy.X - playerX;
                double dy = enemy.Y - playerY;
                double angleToPlayer = Math.Atan2(dy, dx) - playerAngle;

                // Keep angle between -PI and PI
                while (angleToPlayer > Math.PI) angleToPlayer -= 2 * Math.PI;
                while (angleToPlayer < -Math.PI) angleToPlayer += 2 * Math.PI;

                // Skip enemies not in front of player
                if (Math.Abs(angleToPlayer) > Math.PI / 4) continue;
                if (!IsVisible(playerX, playerY, enemy.X, enemy.Y, map)) continue;

                // Position on screen
                int x = (int)((screenWidth / 2) * (1 + Math.Tan(angleToPlayer)));
                int y = screenHeight - fixedSize;

                Bitmap sprite;
                bool flip = false;

                if (enemy.Alive)
                {
                    // Show shooting or walking
                    if (enemy.ShootingAnimationFrames > 0)
                    {
                        sprite = (enemy.ShootingAnimationFrames % 4 < 2) ? EnemyShooting : EnemyReady;
                    }
                    else
                    {
                        (sprite, flip) = GetDirectionalSprite(enemy, playerAngle);
                    }
                }
                else
                {
                    sprite = EnemyDeath2;
                }

                int drawX = x - fixedSize / 2;
                int drawY = y;

                // Flip image if needed
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

        // Update enemy movement and behavior
        public void UpdateEnemies(int[,] map, double playerX, double playerY)
        {
            // Go backwards in case we remove any
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];

                // --- Handle dead enemies ---
                if (!enemy.Alive)
                {
                    // If death timer hasn't been set yet, set it once 
                    if (enemy.DeathTimer == 0)
                        enemy.DeathTimer = 20; 

                    // Count down the timer
                    enemy.DeathTimer--;

                    // Remove only when timer is done
                    if (enemy.DeathTimer <= 0)
                        enemies.RemoveAt(i);

                    // Skip the rest of the update
                    continue;
                }

                // --- Alive enemies update from here ---

                // Check if enemy can see player
                enemy.CanSeePlayer = IsVisible(enemy.X, enemy.Y, playerX, playerY, map);

                double dx = playerX - enemy.X;
                double dy = playerY - enemy.Y;
                double distanceToPlayer = Math.Sqrt(dx * dx + dy * dy);

                if (enemy.ShootingAnimationFrames > 0)
                    enemy.ShootingAnimationFrames--;

                // Move away if too close
                if (enemy.CanSeePlayer && distanceToPlayer < 3.0)
                {
                    double angleAwayFromPlayer = Math.Atan2(-dy, -dx);
                    enemy.LastMoveAngle = angleAwayFromPlayer;

                    double newX = enemy.X + Math.Cos(angleAwayFromPlayer) * enemy.Speed;
                    double newY = enemy.Y + Math.Sin(angleAwayFromPlayer) * enemy.Speed;

                    if (map[(int)newY, (int)newX] == 0)
                    {
                        enemy.X = newX;
                        enemy.Y = newY;
                        enemy.ToggleFrame = !enemy.ToggleFrame;
                    }
                    else
                    {
                        enemy.Direction = new Random().NextDouble() * Math.PI * 2;
                    }

                    enemy.ShootingAnimationFrames = 0;
                }
                // Shoot player if within range
                else if (enemy.CanSeePlayer && distanceToPlayer >= 3.0 && distanceToPlayer < 8.0)
                {
                    if (enemy.ShootCooldown <= 0)
                    {
                        enemy.IsShooting = true;
                        enemy.ShootingAnimationFrames = 15;
                        enemy.ShootCooldown = 60;

                        var enemyShootSound = new SoundPlayer(Properties.Resources.EnemyShootSound);
                        enemyShootSound.Play();

                        if (enemy.CanSeePlayer)
                            OnPlayerHit?.Invoke(10); // Deal 10 damage to player
                    }
                    else
                    {
                        enemy.ShootCooldown--;
                    }
                }
                // Random walking
                else
                {
                    enemy.IsShooting = false;
                    enemy.ShootingAnimationFrames = 0;
                    if (enemy.ShootCooldown > 0) enemy.ShootCooldown--;

                    enemy.LastMoveAngle = enemy.Direction;
                    double newX = enemy.X + Math.Cos(enemy.Direction) * enemy.Speed;
                    double newY = enemy.Y + Math.Sin(enemy.Direction) * enemy.Speed;

                    if (map[(int)enemy.Y, (int)newX] == 0)
                        enemy.X = newX;

                    if (map[(int)newY, (int)enemy.X] == 0)
                        enemy.Y = newY;

                    if (enemy.X != newX || enemy.Y != newY)
                        enemy.ToggleFrame = !enemy.ToggleFrame;
                    else
                        enemy.Direction = new Random().NextDouble() * Math.PI * 2;
                }
            }
        } 

        // Quick access to enemy list
        public List<NPCEnemy> Enemies => enemies;
    }
}
