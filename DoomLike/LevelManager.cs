using System;
using System.Collections.Generic;

namespace DoomLike
{
    internal class LevelManager
    {
        public int CurrentLevel { get; private set; } = 1;
        public int[,] CurrentMap { get; private set; }
        public const int MapWidth = 8;
        public const int MapHeight = 8;

        // List of map layouts
        private List<int[,]> mapLayouts = new List<int[,]>();

        // Spawn points for each map (x, y coordinates)
        private List<List<(double, double)>> spawnPoints = new List<List<(double, double)>>();

        public LevelManager()
        {
            InitializeMaps();
            CurrentMap = mapLayouts[0]; // Start with first map
        }

        private void InitializeMaps()
        {
            // Level 1 - Simple cross pattern
            mapLayouts.Add(new int[,]
            {
                {1,1,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,0,0,1,0,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,0,0,1,0,1},
                {1,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,1},
                {1,1,1,1,1,1,1,1}
            });
            spawnPoints.Add(new List<(double, double)>
            {
                (2.5, 3.5), (3.5, 6.5), (4.5, 1.5)
            });

            // Level 2 - Corner rooms
            mapLayouts.Add(new int[,]
            {
                {1,1,1,1,1,1,1,1},
                {1,0,0,1,1,0,0,1},
                {1,0,0,1,1,0,0,1},
                {1,1,0,0,0,0,1,1},
                {1,1,0,0,0,0,1,1},
                {1,0,0,1,1,0,0,1},
                {1,0,0,1,1,0,0,1},
                {1,1,1,1,1,1,1,1}
            });
            spawnPoints.Add(new List<(double, double)>
            {
                (1.5, 1.5), (6.5, 1.5), (1.5, 6.5), (6.5, 6.5)
            });

            // Level 3 - Maze-like
            mapLayouts.Add(new int[,]
            {
                {1,1,1,1,1,1,1,1},
                {1,0,0,0,1,0,0,1},
                {1,1,1,0,1,0,1,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,1,1}
            });
            spawnPoints.Add(new List<(double, double)>
            {
                (2.5, 1.5), (5.5, 1.5), (3.5, 3.5), (2.5, 5.5), (5.5, 5.5)
            });

            // Level 4 - Central arena
            mapLayouts.Add(new int[,]
            {
                {1,1,1,1,1,1,1,1},
                {1,0,0,1,1,0,0,1},
                {1,0,0,0,0,0,0,1},
                {1,1,0,0,0,0,1,1},
                {1,1,0,0,0,0,1,1},
                {1,0,0,0,0,0,0,1},
                {1,0,0,1,1,0,0,1},
                {1,1,1,1,1,1,1,1}
            });
            spawnPoints.Add(new List<(double, double)>
            {
                (1.5, 1.5), (6.5, 1.5), (3.5, 3.5), (4.5, 4.5), (1.5, 6.5), (6.5, 6.5)
            });

            // Level 5 - Spiral pattern
            mapLayouts.Add(new int[,]
            {
                {1,1,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1},
                {1,0,1,0,0,0,0,1},
                {1,0,1,0,1,1,1,1},
                {1,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1},
                {1,1,1,1,1,1,1,1}
            });
            spawnPoints.Add(new List<(double, double)>
            {
               (2.5, 1.5), (5.5, 1.5),   (5.5, 3.5), (3.5, 3.5),  (2.5, 5.5), (5.5, 5.5)
            });
        }

        // Load the next level
        public void LoadNextLevel(EnemyManager enemyManager)
        {
            CurrentLevel++;

            // Cycle through maps, or use procedural generation for higher levels
            int mapIndex = (CurrentLevel - 1) % mapLayouts.Count;
            CurrentMap = mapLayouts[mapIndex];

            // Calculate enemy count based on level (3 + level number)
            int enemyCount = 3 + CurrentLevel;
            List<(double, double)> spawns = spawnPoints[mapIndex];

            // Spawn enemies at designated spawn points
            enemyManager.ClearEnemies();

            // Use all spawn points, then add random ones if we need more enemies
            for (int i = 0; i < enemyCount; i++)
            {
                if (i < spawns.Count)
                {
                    enemyManager.SpawnEnemy(spawns[i].Item1, spawns[i].Item2);
                }
                else
                {
                    // Find a random valid spawn location
                    var (x, y) = GetRandomSpawnPoint();
                    enemyManager.SpawnEnemy(x, y);
                }
            }
        }

        // Get player starting position for current level
        public (double x, double y, double angle) GetPlayerStartPosition()
        {
            // Start player in a safe location based on the map
            // Default is near the center, but you can customize per level
            return (3.5, 3.5, 0);
        }

        // Find a random valid spawn point in the current map
        private (double x, double y) GetRandomSpawnPoint()
        {
            Random rand = new Random();
            int attempts = 0;

            while (attempts < 100) // Prevent infinite loop
            {
                int x = rand.Next(1, MapWidth - 1);
                int y = rand.Next(1, MapHeight - 1);

                // Check if this location is walkable
                if (CurrentMap[y, x] == 0)
                {
                    return (x + 0.5, y + 0.5);
                }
                attempts++;
            }

            // Fallback to a default position
            return (3.5, 3.5);
        }

        // Check if all enemies are dead and we should progress
        public bool ShouldLoadNextLevel(EnemyManager enemyManager)
        {
            return enemyManager.Enemies.Count == 0;
        }

        // Get level information for display
        public string GetLevelInfo()
        {
            return $"Level {CurrentLevel}";
        }

        // Reset to level 1 (for game over/restart)
        public void Reset(EnemyManager enemyManager)
        {
            CurrentLevel = 1;
            CurrentMap = mapLayouts[0];

            // Spawn initial enemies
            enemyManager.ClearEnemies();
            foreach (var spawn in spawnPoints[0])
            {
                enemyManager.SpawnEnemy(spawn.Item1, spawn.Item2);
            }
        }
    }
}