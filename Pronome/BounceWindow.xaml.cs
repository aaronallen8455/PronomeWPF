using System;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using Pronome.Bounce;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BounceWindow.xaml
    /// </summary>
    public partial class BounceWindow : Window
    {
        /// <summary>
        /// Is true if the window is not hidden.
        /// </summary>
        public bool SceneDrawn = false;

        protected DrawingVisual Drawing = new DrawingVisual();

        public static BounceWindow Instance;

        public BounceWindow()
        {
            Instance = this;
            InitializeComponent();
            Helper.Drawing = Drawing;
            DrawScene();
            AddVisualChild(Drawing);
            AddLogicalChild(Drawing);
            Metronome.AfterBeatParsed += new EventHandler(DrawScene);
        }

        protected void DrawScene(object sender, EventArgs e)
        {
            if (SceneDrawn)
            {
                DrawScene();
            }
        }

        /// <summary>
        /// Draw the scene components
        /// </summary>
        public void DrawScene()
        {
            Helper.DrawScene();

            // attach to frame rendering event
            CompositionTarget.Rendering -= Helper.DrawFrame;
            CompositionTarget.Rendering += Helper.DrawFrame;

            SceneDrawn = true;
        }

        /// <summary>
        /// Handle the frame render event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DrawFrame(object sender, EventArgs e)
        {
            //Helper.DrawFrame(Drawing);
        }

        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;

                SceneDrawn = false;
                CompositionTarget.Rendering -= DrawFrame;
            }
        }

        protected override int VisualChildrenCount
        {
            get => 1;
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException("index");

            return Drawing;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            DrawScene();
        }
    }
}