using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace YousicianUnlimited
{
	class MainWindowViewModel : INotifyPropertyChanged
    {
        private string _startDate = @"2020/01/01 00:00:00";
        public string StartDate
        {
            get => _startDate;
            set
            {
                if (value != _startDate)
                {
                    _startDate = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _lastDate = @"2020/01/01 00:00:00";
        public string LastDate
        {
            get => _lastDate;
            set
            {
                if (value != _lastDate)
                {
                    _lastDate = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ulong _shift;
        public ulong Shift
        {
            get => _shift;
            set
            {
                if (value != _shift)
                {
                    _shift = value;
                    RaisePropertyChanged();
                }
            }
        }

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
                }
            }
        }

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
