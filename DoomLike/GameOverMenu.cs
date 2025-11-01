using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLike
{
    public class GameOverMenu : Form
    {
        // our game over form
        public void GameOverForm()
        {
            this.Text = "GAME OVER";
            this.Size = new Size ( 400, 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.Black;

            Label gameoverlabel = new Label
            {
                Text = "YOU HAVE BEEN DEFEATED",
                ForeColor = Color.Red,
                Font = new Font("Arial", 24, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            this.Controls.Add(gameoverlabel);

        }

    }
}
