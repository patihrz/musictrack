using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Control = System.Windows.Controls.Control;

namespace SpotifyNowPlayingOverlay.Controls
{
    [TemplatePart(Name = "PART_Canvas", Type = typeof(Canvas))]
    [TemplatePart(Name = "PART_TextBlock", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_Translate", Type = typeof(TranslateTransform))]
    public class MarqueeTextBlock : Control
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private Canvas? _canvas;
        private TextBlock? _textBlock;
        private TranslateTransform? _translateTransform;
        private Storyboard? _storyboard;

        static MarqueeTextBlock()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(typeof(MarqueeTextBlock)));
        }

        public MarqueeTextBlock()
        {
            // Programmatically define the template using XamlReader to make the control self-contained.
            string templateXml = @"
                <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                 xmlns:local='clr-namespace:SpotifyNowPlayingOverlay.Controls;assembly=SpotifyNowPlayingOverlay'
                                 TargetType='local:MarqueeTextBlock'>
                    <Border Background='Transparent' ClipToBounds='True'>
                        <Canvas x:Name='PART_Canvas' ClipToBounds='True' Background='Transparent' Height='{TemplateBinding Height}'>
                            <TextBlock x:Name='PART_TextBlock' 
                                       Text='{TemplateBinding Text}' 
                                       Foreground='{TemplateBinding Foreground}'
                                       FontFamily='{TemplateBinding FontFamily}'
                                       FontWeight='{TemplateBinding FontWeight}'
                                       FontSize='{TemplateBinding FontSize}'
                                       TextWrapping='NoWrap'
                                       VerticalAlignment='Center'>
                                <TextBlock.RenderTransform>
                                    <TranslateTransform x:Name='PART_Translate' X='0' Y='0'/>
                                </TextBlock.RenderTransform>
                            </TextBlock>
                        </Canvas>
                    </Border>
                </ControlTemplate>";

            Template = (ControlTemplate)XamlReader.Parse(templateXml);

            Loaded += MarqueeTextBlock_Loaded;
            Unloaded += MarqueeTextBlock_Unloaded;
            SizeChanged += MarqueeTextBlock_SizeChanged;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _canvas = GetTemplateChild("PART_Canvas") as Canvas;
            _textBlock = GetTemplateChild("PART_TextBlock") as TextBlock;
            _translateTransform = _textBlock?.RenderTransform as TranslateTransform;

            if (_textBlock != null)
            {
                _textBlock.SizeChanged += TextBlock_SizeChanged;
            }

            UpdateMarquee();
        }

        private void MarqueeTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMarquee();
        }

        private void MarqueeTextBlock_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void MarqueeTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMarquee();
        }

        private void TextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMarquee();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock marquee)
            {
                marquee.UpdateMarquee();
            }
        }

        private void UpdateMarquee()
        {
            if (_canvas == null || _textBlock == null || _translateTransform == null) return;

            // Force layout update to get correct sizes
            _textBlock.UpdateLayout();
            _canvas.UpdateLayout();

            double containerWidth = ActualWidth;
            double textWidth = _textBlock.ActualWidth;

            // Make sure height of canvas matches text height if not set
            if (double.IsNaN(Height) || Height == 0)
            {
                _canvas.Height = _textBlock.ActualHeight;
            }

            if (containerWidth > 0 && textWidth > containerWidth)
            {
                StartAnimation(textWidth, containerWidth);
            }
            else
            {
                StopAnimation();
            }
        }

        private void StartAnimation(double textWidth, double containerWidth)
        {
            StopAnimation();

            if (_translateTransform == null) return;

            double scrollDistance = textWidth - containerWidth + 24; // Scroll slightly past for visual comfort
            double speed = 30.0; // Pixels per second
            double scrollDuration = scrollDistance / speed;

            var animation = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(animation, _textBlock);
            Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));

            // KeyFrame 1: Start at 0, pause for 1 second
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));

            // KeyFrame 2: Scroll smoothly to -scrollDistance
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1 + scrollDuration))));

            // KeyFrame 3: Pause for 1 second at the end
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + scrollDuration))));

            // KeyFrame 4: Snap back to 0 quickly (0.2s)
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.2 + scrollDuration))));

            _storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _storyboard.Children.Add(animation);
            _storyboard.Begin(this, true);
        }

        private void StopAnimation()
        {
            if (_storyboard != null)
            {
                _storyboard.Stop(this);
                _storyboard = null;
            }

            if (_translateTransform != null)
            {
                _translateTransform.X = 0;
            }
        }
    }
}
