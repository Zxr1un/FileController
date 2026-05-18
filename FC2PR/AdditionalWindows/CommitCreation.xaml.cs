using FileController_v2.VC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FileController_v2
{
    /// <summary>
    /// Логика взаимодействия для CommitCreation.xaml
    /// </summary>
    public partial class CommitCreation : Window
    {
        private Commit _commit;
        
        public CommitCreation(Commit commit)
        {
           
            InitializeComponent();
            _commit = commit;
            CommitID.Text = commit.ID;
            CommitParentID.Text = commit.ParentID;
            CommitName.Text = commit.Name;
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _commit.Name = CommitName.Text;
            DialogResult = true;
            Close();
        }


    }
}
