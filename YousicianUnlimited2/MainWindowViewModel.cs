using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace YousicianUnlimited
{
	class MainWindowViewModel : INotifyPropertyChanged
    {
        private string _startDate = @"2020/01/01 00h 00m 00s";
        public string StartDate
        {
            get => _startDate.Replace(@"{0}", @"h ").Replace(@"{1}", @"m ").Replace(@"{2}", @"s ");
            set
            {
                if (value != _startDate)
                {
                    _startDate = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _remainingTime = @"00h 00m 00s";
        public string RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (value != _remainingTime)
                {
                    _remainingTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _started = false;
        public bool Started
        {
            get => _started;
            set
            {
                if (value != _started)
                {
                    _started = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(@"StartButtonEnabled");
                    RaisePropertyChanged(@"StopButtonEnabled");
                }
            }
        }

        private bool _isTimerRunning;
        public bool IsTimerRunning
        {
            get => _isTimerRunning;
            set
            {
                if (value != _isTimerRunning)
                {
                    _isTimerRunning = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(@"PauseButtonCaption");
                }
            }
        }

        public string PauseButtonCaption => IsTimerRunning ? @"Pause" : @"Resume";

        private bool _notClosing = true;
        public bool NotClosing
        {
            get => _notClosing;
            set
            {
                if (value != _notClosing)
                {
                    _notClosing = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(@"VisibilityOnClosing");
                    RaisePropertyChanged(@"StartButtonEnabled");
                    RaisePropertyChanged(@"StopButtonEnabled");
                }
            }
        }

        public bool StartButtonEnabled => NotClosing && !Started;

        public bool StopButtonEnabled => NotClosing && Started;

        public Visibility VisibilityOnClosing
        {
            get => _notClosing ? Visibility.Collapsed : Visibility.Visible;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] String propertyName = null)
        {
            AssertPropertyExists(propertyName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void UpdateProperty<T>(ref T backingField, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (Equals(backingField, newValue))
            {
                return;
            }

            backingField = newValue;
            RaisePropertyChanged(propertyName);
        }

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public virtual void AssertPropertyExists(string propertyName)
        {
            // Verify that the property name matches a real,
            // public, instance property on this object.
            var properties = TypeDescriptor.GetProperties(this);
            if (properties[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;
                Debug.Fail(msg);
            }
        }
    }
}
