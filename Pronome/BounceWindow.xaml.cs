using System;
using System.Collections.Generic;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BounceWindow.xaml
    /// </summary>
    public partial class BounceWindow : Window
    {
        /// <summary>
        /// The window instance.
        /// </summary>
        public static BounceWindow Instance;

        /// <summary>
        /// Is true if the window is not hidden.
        /// </summary>
        public bool SceneDrawn = false;

        protected DrawingVisual Drawing = new DrawingVisual();

        protected Bounce.Helper Helper;

        public BounceWindow()
        {
            Instance = this;
            Helper = new Bounce.Helper(Drawing);
            InitializeComponent();
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

        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;

                SceneDrawn = false;
                CompositionTarget.Rendering -= Helper.DrawFrame;
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
