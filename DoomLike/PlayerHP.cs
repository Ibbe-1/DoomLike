using System;
using System.Collections.Generic;
using System.Drawing;

namespace DoomLike
{
    internal class PlayerHP
    {
        // List of floating texts
        private List<FloatingText> floatingTexts = new List<FloatingText>();

        // Nested class for floating numbers
        internal class FloatingText
        {
            public string Text;
            public Color Color;
            public float X, Y;
            public int Lifetime;

            // the lifetime is how long the text will be displayed for
            public FloatingText(string text, Color color, float x, float y, int lifetime = 25)
            {
                Text = text;
                Color = color;
                X = x;
                Y = y;
                Lifetime = lifetime;
            }

            public void Update()
            {
                Y -= 2f; // the speed at which the text floats up
                Lifetime--;
            }

            public void Draw(Graphics g)
            {
                using (Font font = new Font("Arial", 12, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(Color))
                {
                    g.DrawString(Text, font, brush, X, Y);
                }
            }

            public bool IsAlive => Lifetime > 0;
        }

        // Call this when the player takes damage
        public void ShowDamage(int amount)
        {
            floatingTexts.Add(new FloatingText($"-{amount}", Color.Red, 20, 400));
        }

        // Call this when the player heals
        public void ShowHeal(int amount)
        {
            floatingTexts.Add(new FloatingText($"+{amount}", Color.Lime, 20, 400));
        }

        // Draw health bar and floating texts
        public void Draw(Graphics g, int playerHealth, int maxHealth, int screenHeight)
        {
            int barWidth = 200;
            int barHeight = 20;
            int barX = 10;
            int barY = screenHeight - barHeight - 10;

            float healthPercent = (float)playerHealth / maxHealth;
            int healthWidth = (int)(barWidth * healthPercent);

            g.FillRectangle(Brushes.DarkRed, barX, barY, barWidth, barHeight);
            g.FillRectangle(Brushes.LimeGreen, barX, barY, healthWidth, barHeight);
            g.DrawRectangle(Pens.Black, barX, barY, barWidth, barHeight);

            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                string hpText = $"HP: {playerHealth}/{maxHealth}";
                g.DrawString(hpText, font, textBrush, barX + 5, barY - 18);
            }

            // Update and draw floating texts
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                floatingTexts[i].Update();
                floatingTexts[i].Draw(g);
                if (!floatingTexts[i].IsAlive)
                    floatingTexts.RemoveAt(i);
            }
        }
    }
}
