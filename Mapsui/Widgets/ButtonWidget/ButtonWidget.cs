﻿using Mapsui.Geometries;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mapsui.Widgets.Button
{
    /// <summary>
    /// Widget which shows a buttons
    /// </summary>
    /// <remarks>
    /// With this, the user could add buttons with SVG icons to the map.
    /// 
    /// Usage
    /// To show a ButtonWidget, add a instance of the ButtonWidget to Map.Widgets by
    /// 
    ///   map.Widgets.Add(new ButtonWidget(map, picture));
    ///   
    /// Customize
    /// Picture: SVG image to display for button
    /// Rotation: Value for rotation in degrees
    /// Opacity: Opacity of button
    /// </remarks>
    public class ButtonWidget : Widget, INotifyPropertyChanged
    {
        /// <summary>
        /// Event handler which is called, when the button is touched
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Event handler which is called, when the button is touched
        /// </summary>
        public event EventHandler<WidgetTouchedEventArgs> WidgetTouched;

        private string _svgImage;

        /// <summary>
        /// SVG image to show for button
        /// </summary>
        public string SVGImage
        {
            get
            {
                return _svgImage;
            }
            set
            {
                if (_svgImage == value)
                    return;

                _svgImage = value;
                Picture = null;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Object for prerendered image. For internal use only.
        /// </summary>
        public object Picture { get; set; }

        private float _rotation = 0;

        /// <summary>
        /// Rotation of the SVG image
        /// </summary>
        public float Rotation
        {
            get
            {
                return _rotation;
            }
            set
            {
                if (_rotation == value)
                    return;
                _rotation = value;
                OnPropertyChanged();
            }
        }

        private float _opacity = 0.8f;

        /// <summary>
        /// Opacity of background, frame and signs
        /// </summary>
        public float Opacity
        {
            get
            {
                return _opacity;
            }
            set
            {
                if (_opacity == value)
                    return;
                _opacity = value;
                OnPropertyChanged();
            }
        }

        public override bool HandleWidgetTouched(INavigator navigator, Point position)
        {
            var args = new WidgetTouchedEventArgs(position);

            WidgetTouched?.Invoke(this, args);

            return args.Handled;
        }

        internal void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}