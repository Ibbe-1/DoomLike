using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Forms;

namespace DoomLike
{
    public class MainMenu : Form
    {

        private Button startButton;
        private Button exitButton;
        public MainMenu()
        {
            BackColor = Color.Gray; 

            BackgroundImage = Properties.Resources.GameConsole;
            BackgroundImageLayout = ImageLayout.Stretch;


            Label infoLabel = new Label
            {
                Text = "Weapon, NPC and wall sprite resources belong to ID software.\n" +
           "This program contains both background music from Oldschool Runescape produced by Jagex " +
           "and Doom 2 by ID software.",
                AutoSize = true,
                MaximumSize = new Size(350, 0), 
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(25, 10),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            Controls.Add(infoLabel);


            Text = "DoomLike - Menu ";
            ClientSize = new Size(400, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Start Game button
            startButton = new Button
            {
                Text = "Start Game",
                Size = new Size(120, 40),
                Location = new Point((ClientSize.Width - 120) / 2, 100)
            };
            startButton.Click += StartButton_Click;
            Controls.Add(startButton);

            // Exit button
            exitButton = new Button
            {
                Text = "Exit",
                Size = new Size(120, 40),
                Location = new Point((ClientSize.Width - 120) / 2, 160)
            };
            exitButton.Click += ExitButton_Click;
            Controls.Add(exitButton);

            // Info label at the bottom
            Label info = new Label
            {
                Text = "For fun hobby project made by Ibbe-1",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = ClientSize.Width,
                Height = 30,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            info.Location = new Point(
                (ClientSize.Width - info.Width) / 2,
                ClientSize.Height - info.Height - 10
            );

            Controls.Add(info);
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            using (DoomLikeGame game = new DoomLikeGame(this))
            {
                this.Hide();
                game.ShowDialog(); // modal game window
                this.Show();       
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
