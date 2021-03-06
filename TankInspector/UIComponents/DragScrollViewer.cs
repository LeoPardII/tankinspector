﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Smellyriver.TankInspector.UIComponents
{
    public class DragScrollViewer : ScrollViewer
    {
        private double _friction = DefaultFriction;
        public double Friction
        {
            get => _friction;
	        set => _friction = Math.Min(Math.Max(value, MinimumFriction), MaximumFriction);
        }

	    public double DragStartSensitivity { get; set; } = 2.0;

	    private Point _beginPoint;
        private Point _previousPreviousPoint;
        private Point _previousPoint;
        private Point _currentPoint;

        private bool _mouseDown = false;
        private bool _isDragging = false;

        private bool _isListeningCompositionTargetRendering;

        public event EventHandler DragBegin;
        public event EventHandler DragEnd;
        public event EventHandler SlideEnd;

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            this.CancelDrag(PreviousVelocity);
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _beginPoint = _currentPoint = _previousPoint = _previousPreviousPoint = e.GetPosition(this);
            this.Momentum = new Vector(0, 0);
            _mouseDown = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            _currentPoint = e.GetPosition(this);
            if (_mouseDown && !_isDragging)
            {
                if ((_currentPoint - _beginPoint).Length >= this.DragStartSensitivity)
                {
                    this.BeginDrag();
                    e.Handled = true;

                    _isDragging = true;
                    DragScroll();
                }
            }
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            if (_isDragging)
                e.Handled = true;
            CancelDrag(PreviousVelocity);
        }

        private void OnDragBegin()
        {
            if (this.DragBegin != null)
                this.DragBegin(this, new EventArgs());
        }

        private void BeginDrag()
        {
            this.Cursor = Cursors.Hold;
            this.CaptureMouse();
            this.OnDragBegin();
        }

        private void OnDragEnd()
        {
            if (this.DragEnd != null)
                this.DragEnd(this, new EventArgs());
        }

        private void CancelDrag(Vector velocityToUse)
        {
            if (_isDragging)
                Momentum = velocityToUse;
            _isDragging = false;
            _mouseDown = false;
            Cursor = Cursors.Drag;

            this.ReleaseMouseCapture();

            this.OnDragEnd();
        }


        protected void DragScroll()
        {
            if (!_isListeningCompositionTargetRendering)
            {
                CompositionTarget.Rendering += CompositionTarget_Rendering;
                _isListeningCompositionTargetRendering = true;
            }
        }

        private void OnSlideEnd()
        {
            if (this.SlideEnd != null)
                this.SlideEnd(this, new EventArgs());
        }

	    private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_isDragging)
            {

                GeneralTransform generalTransform = this.TransformToVisual(this);
                Point childToParentCoordinates = generalTransform.Transform(new Point(0, 0));
                Rect bounds = new Rect(childToParentCoordinates, this.RenderSize);

                if (bounds.Contains(_currentPoint))
                {
                    PerformScroll(PreviousVelocity);
                }

                if (!_mouseDown)
                {
                    CancelDrag(Velocity);
                }
                _previousPreviousPoint = _previousPoint;
                _previousPoint = _currentPoint;
            }
            else if (Momentum.Length > 0.01)
            {
                Momentum *= (1.0 - _friction / 4.0);
                if (!PerformScroll(Momentum))
                    this.StopSlide();
            }
            else
                this.StopSlide();
        }


        private void StopSlide()
        {
            if (_isListeningCompositionTargetRendering)
            {
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                _isListeningCompositionTargetRendering = false;

                this.OnSlideEnd();
            }
        }


        private void CancelDrag()
        {
            _isDragging = false;
            Momentum = Velocity;
        }

        private static double CoerceOffset(double offset, double extent, double viewport)
        {
            if (offset > (extent - viewport))
                offset = extent - viewport;
            if (offset < 0.0)
                offset = 0.0;

            return offset;
        }

        private void CoerceOffsets(ref double horizontalOffset, ref double verticalOffset)
        {
            horizontalOffset = DragScrollViewer.CoerceOffset(horizontalOffset, this.ExtentWidth, this.ViewportWidth);
            verticalOffset = DragScrollViewer.CoerceOffset(verticalOffset, this.ExtentHeight, this.ViewportHeight);
        }


        private bool PerformScroll(Vector displacement)
        {
            var horizontalOffset = this.HorizontalOffset - displacement.X;
            var verticalOffset = this.VerticalOffset - displacement.Y;

            this.CoerceOffsets(ref horizontalOffset, ref verticalOffset);

            ScrollToVerticalOffset(verticalOffset);
            ScrollToHorizontalOffset(horizontalOffset);

            return Math.Abs(horizontalOffset - this.HorizontalOffset) > 1.0
                || Math.Abs(verticalOffset - this.VerticalOffset) > 1.0;
        }

        private const double DragPollingInterval = 1; // milliseconds
        private const double DefaultFriction = 0.2;
        private const double MinimumFriction = 0.0;
        private const double MaximumFriction = 1.0;


        private Vector Momentum { get; set; }
        private Vector Velocity => new Vector(_currentPoint.X - _previousPoint.X, _currentPoint.Y - _previousPoint.Y);

	    // Using PreviousVelocity gives a smoother, better feeling as it leaves out any last frame momentum changes
        private Vector PreviousVelocity => new Vector(_previousPoint.X - _previousPreviousPoint.X, _previousPoint.Y - _previousPreviousPoint.Y);

	    private class Vector
        {
            public double Length => Math.Sqrt(X * X + Y * Y);

	        public Vector(double x, double y)
            {
                X = x;
                Y = y;
            }

            public static Vector operator *(Vector vector, double scalar)
            {
                return new Vector(vector.X * scalar, vector.Y * scalar);
            }

            public double X { get; }
            public double Y { get; }

        }
    }
}
