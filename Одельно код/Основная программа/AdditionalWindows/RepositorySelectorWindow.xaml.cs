using System.Windows;
using FileController_v2.VC;

namespace FileController_v2
{
    public partial class RepositorySelection : Window
    {
        public Repository? SelectedRepository { get; private set; }

        public RepositorySelection()
        {
            InitializeComponent();

            RepoList.ItemsSource =
                MainProgramLogic.Repositories;
        }

        private void RepoList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SelectedRepository = RepoList.SelectedItem as Repository;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRepository == null)  return;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedRepository = null;
            DialogResult = false;
            Close();
        }
    }
}