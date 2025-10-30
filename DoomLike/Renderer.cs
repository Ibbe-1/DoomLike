using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DoomLike
{
    internal class Renderer
    {
        private const int ScreenWidth = 640;
        private const int ScreenHeight = 480;
        private const int MapWidth = 8;
        private const int MapHeight = 8;

        private Bitmap buffer;

        public Renderer()
        {
            buffer = new Bitmap(ScreenWidth, ScreenHeight);
        }

        // Main render method that handles all drawing
        public void Render(
            Graphics targetGraphics,
            Bitmap skybox,
            Bitmap floorSprite,
            Bitmap wallSprite,
            Bitmap currentWeaponSprite,
            int[,] map,
            double playerX,
            double playerY,
            double playerAngle,
            EnemyManager enemyManager)
        {
            // Lock the buffer so we can write pixels to it directly
            var bufferData = buffer.LockBits(new Rectangle(0, 0, buffer.Width, buffer.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = bufferData.Stride;
            byte[] pixelBuffer = new byte[Math.Abs(stride) * buffer.Height];

            // Lock textures - we need to lock them to read their pixel data fast
            var (skyPixels, skyData, skyStride) = LockTexture(skybox);
            var (floorPixels, floorData, floorStride) = LockTexture(floorSprite);
            var (wallPixels, wallData, wallStride) = LockTexture(wallSprite);

            // Draw skybox (ceiling) - just tiles the skybox texture across the top half
            DrawSkybox(pixelBuffer, stride, skyPixels, skyStride, skybox);

            // Draw floor using floor texture
            DrawFloor(pixelBuffer, stride, floorPixels, floorStride, floorSprite, playerX, playerY, playerAngle);

            // Draw walls - main raycasting part for the walls
            DrawWalls(pixelBuffer, stride, wallPixels, wallStride, wallSprite, map, playerX, playerY, playerAngle);

            // Copy back to buffer and unlock
            Marshal.Copy(pixelBuffer, 0, bufferData.Scan0, pixelBuffer.Length);
            buffer.UnlockBits(bufferData);
            skybox.UnlockBits(skyData);
            floorSprite.UnlockBits(floorData);
            wallSprite.UnlockBits(wallData);

            // Draw enemies - enemies are drawn as sprites on top of the 3D world
            using (Graphics g = Graphics.FromImage(buffer))
                enemyManager.DrawEnemies(g, playerX, playerY, playerAngle, ScreenWidth, ScreenHeight, map);

            // Render buffer to screen
            targetGraphics.DrawImage(buffer, 0, 0);

            // Draw weapon - weapon is drawn on top of everything else
            targetGraphics.DrawImage(currentWeaponSprite, (ScreenWidth - 220) / 2, ScreenHeight - 200, 220, 200);
        }

        private void DrawSkybox(byte[] pixelBuffer, int stride, byte[] skyPixels, int skyStride, Bitmap skybox)
        {
            for (int y = 0; y < ScreenHeight / 2; y++)
                for (int x = 0; x < ScreenWidth; x++)
                    CopyPixel(pixelBuffer, y * stride + x * 4, skyPixels,
                        (y % skybox.Height) * skyStride + (x % skybox.Width) * 4);
        }

        private void DrawFloor(byte[] pixelBuffer, int stride, byte[] floorPixels, int floorStride,
            Bitmap floorSprite, double playerX, double playerY, double playerAngle)
        {
            for (int y = ScreenHeight / 2; y < ScreenHeight; y++)
            {
                // Calculate the ray directions for the left and right edges of the screen
                double angle0 = playerAngle - Math.PI / 4, angle1 = playerAngle + Math.PI / 4;
                double rayDirX0 = Math.Cos(angle0), rayDirY0 = Math.Sin(angle0);
                double rayDirX1 = Math.Cos(angle1), rayDirY1 = Math.Sin(angle1);

                // Figure out how far away this row of floor is from the player
                double rowDistance = 0.5 * ScreenHeight / (y - ScreenHeight / 2);

                // Calculate how much to step across the floor for each pixel
                double floorStepX = rowDistance * (rayDirX1 - rayDirX0) / ScreenWidth;
                double floorStepY = rowDistance * (rayDirY1 - rayDirY0) / ScreenWidth;

                // Starting position for this row of floor
                double floorX = playerX + rowDistance * rayDirX0;
                double floorY = playerY + rowDistance * rayDirY0;

                for (int x = 0; x < ScreenWidth; x++)
                {
                    // Get the texture coordinates
                    int tx = (int)(floorSprite.Width * (floorX - Math.Floor(floorX))) & (floorSprite.Width - 1);
                    int ty = (int)(floorSprite.Height * (floorY - Math.Floor(floorY))) & (floorSprite.Height - 1);

                    // Copy the pixel from the floor texture to the screen
                    CopyPixel(pixelBuffer, y * stride + x * 4, floorPixels, ty * floorStride + tx * 4);

                    // Move to the next floor position
                    floorX += floorStepX;
                    floorY += floorStepY;
                }
            }
        }

        private void DrawWalls(byte[] pixelBuffer, int stride, byte[] wallPixels, int wallStride,
            Bitmap wallSprite, int[,] map, double playerX, double playerY, double playerAngle)
        {
            for (int x = 0; x < ScreenWidth; x++)
            {
                // Calculate the ray angle for this vertical strip of screen
                double rayAngle = playerAngle + Math.Atan(2 * x / (double)ScreenWidth - 1);
                double rayDirX = Math.Cos(rayAngle), rayDirY = Math.Sin(rayAngle);

                // Start at the player position
                int mapX = (int)playerX, mapY = (int)playerY;

                // Figure out how far the ray has to travel to cross one grid square
                double deltaDistX = Math.Abs(1 / rayDirX), deltaDistY = Math.Abs(1 / rayDirY);

                // Which direction to step in (1 or -1)
                int stepX = rayDirX < 0 ? -1 : 1, stepY = rayDirY < 0 ? -1 : 1;

                // Calculate distance to the next grid line in each direction
                double sideDistX = (rayDirX < 0 ? playerX - mapX : mapX + 1.0 - playerX) * deltaDistX;
                double sideDistY = (rayDirY < 0 ? playerY - mapY : mapY + 1.0 - playerY) * deltaDistY;

                // Keeps track of if we hit a horizontal or vertical wall
                bool raySide = false;

                // DDA algorithm - step through the grid until we hit a wall
                while (true)
                {
                    // Check which direction is closer and step that way
                    if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; raySide = false; }
                    else { sideDistY += deltaDistY; mapY += stepY; raySide = true; }

                    // If we hit a wall or went out of bounds, stop
                    if (mapX < 0 || mapX >= MapWidth || mapY < 0 || mapY >= MapHeight || map[mapY, mapX] > 0) break;
                }

                // Calculate perpendicular distance to avoid fisheye effect
                double perpWallDist = raySide ? (sideDistY - deltaDistY) : (sideDistX - deltaDistX);

                // Figure out how tall to draw the wall
                int lineHeight = (int)(ScreenHeight / perpWallDist);

                // Calculate where to start and stop drawing the wall on screen
                int drawStart = Math.Max(0, -lineHeight / 2 + ScreenHeight / 2);
                int drawEnd = Math.Min(ScreenHeight - 1, lineHeight / 2 + ScreenHeight / 2);

                // Figure out where we hit the wall
                double wallX = (raySide ? playerX + perpWallDist * rayDirX : playerY + perpWallDist * rayDirY) -
                    Math.Floor(raySide ? playerX + perpWallDist * rayDirX : playerY + perpWallDist * rayDirY);

                // Convert to texture x coordinate
                int texX = (int)(wallX * wallSprite.Width);

                // Flip the texture for certain wall sides
                if ((!raySide && rayDirX > 0) || (raySide && rayDirY < 0)) texX = wallSprite.Width - texX - 1;

                // Draw each pixel of the wall column
                for (int y = drawStart; y < drawEnd; y++)
                {
                    // Figure out which y coordinate of the texture to use
                    int texY = (((y * 256 - ScreenHeight * 128 + lineHeight * 128) * wallSprite.Height) / lineHeight) / 256;

                    // Make sure we don't go out of bounds on the texture
                    texY = Math.Max(0, Math.Min(texY, wallSprite.Height - 1));
                    texX = Math.Max(0, Math.Min(texX, wallSprite.Width - 1));

                    // Copy the pixel from wall texture to screen
                    CopyPixel(pixelBuffer, y * stride + x * 4, wallPixels, texY * wallStride + texX * 4);
                }
            }
        }

        // Helper method to lock a texture and get its pixel data
        private (byte[], BitmapData, int) LockTexture(Bitmap texture)
        {
            var data = texture.LockBits(new Rectangle(0, 0, texture.Width, texture.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[Math.Abs(data.Stride) * texture.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return (pixels, data, data.Stride);
        }

        // Helper to copy a pixel from one array to another - copies BGRA values
        private void CopyPixel(byte[] dest, int dIdx, byte[] src, int sIdx)
        {
            dest[dIdx] = src[sIdx];         // B
            dest[dIdx + 1] = src[sIdx + 1]; // G
            dest[dIdx + 2] = src[sIdx + 2]; // R
            dest[dIdx + 3] = src[sIdx + 3]; // A
        }

        // Cleanup method
        public void Dispose()
        {
            buffer?.Dispose();
        }
    }
}