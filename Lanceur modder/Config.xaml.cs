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
using Microsoft.VisualBasic.Devices;

namespace Lanceur_modder
{
    /// <summary>
    /// Logique d'interaction pour Config.xaml
    /// </summary>
    /// 

    public partial class Config : Window
    {
        public Config()
        {
            InitializeComponent();
            ComputerInfo HI = new ComputerInfo();
            long mem = MapUlongToLong(HI.TotalPhysicalMemory) / 1000000000;
            SL_RAM.Maximum = mem;
        }

        public static long MapUlongToLong(ulong ulongValue)
        {
            return unchecked((long)ulongValue);
        }

        private void SL_RAM_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (L_RAM != null)
            {
                L_RAM.Content = e.NewValue + " Go";
            }
        }

        private void BT_OK_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
