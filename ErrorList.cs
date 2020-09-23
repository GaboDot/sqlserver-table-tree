using System.Collections.Generic;
using System.Windows.Forms;

namespace TableLookUp
{
    public partial class ErrorWindow : Form
    {
        public ErrorWindow(List<string> errores)
        {
            InitializeComponent();
            foreach(string e in errores)
            {
                errorList.Items.Add(e);
            }
        }
    }
}
