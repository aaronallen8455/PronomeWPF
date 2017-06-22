using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections;

namespace Pronome
{
    public partial class ComboBoxFiltered : ComboBox
    {
        /// <summary>
        /// Gets or sets the search filter field value.
        /// </summary>
        public string SearchFilter
        {
            get => (string)GetValue(SearchFilterProperty);
            set => SetValue(SearchFilterProperty, value);
        }

        public readonly static DependencyProperty SearchFilterProperty =
            DependencyProperty.Register("SearchFilter", typeof(string), typeof(ComboBoxFiltered),
                new PropertyMetadata(string.Empty, new PropertyChangedCallback(SearchFilter_PropertyChanged)));

        static ComboBoxFiltered()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ComboBoxFiltered), new FrameworkPropertyMetadata(typeof(ComboBoxFiltered)));
        }

        public ComboBoxFiltered()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Refresh the view when the property is changed.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void SearchFilter_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ComboBoxFiltered)d).RefreshFilter();
        }

        /// <summary>
        /// Refresh the filter.
        /// </summary>
        private void RefreshFilter()
        {
            if (ItemsSource != null)
            {
                var view = CollectionViewSource.GetDefaultView(ItemsSource);

                view.Refresh();
            }
        }

        /// <summary>
        /// The predicate for filtering the items.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool FilterPredicate(object value)
        {
            if (value == null)
                return false;

            if (string.IsNullOrEmpty(SearchFilter))
                return true;

            return value.ToString().ToLower().Contains(SearchFilter.ToLower());
        }

        /// <summary>
        /// Add the filter to the view
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            if (newValue != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(newValue);
                view.Filter += FilterPredicate;
            }
            
            if (oldValue != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(oldValue);
                view.Filter -= FilterPredicate;
            }

            base.OnItemsSourceChanged(oldValue, newValue);
        }

        /// <summary>
        /// Remove filter when dopdown closes
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDropDownClosed(EventArgs e)
        {
            SearchFilter = string.Empty;

            base.OnDropDownClosed(e);
        }

        public bool AllowNull = false;

        /// <summary>
        /// Don't let the selection become NULL
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (!AllowNull && e.AddedItems.Count == 0)
            {
                e.Handled = true;
                return;
            }

            base.OnSelectionChanged(e);
        }
    }
}
