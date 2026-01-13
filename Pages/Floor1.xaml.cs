using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kiosk.Pages
{
    public partial class Floor1 : Page
    {
        public Floor1()
        {
            InitializeComponent();
        }

        private void Room_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string roomNumber)
            {
                var floorPlanWindow = Window.GetWindow(this) as FloorPlanWindow;
                floorPlanWindow?.ShowRoomInfo(roomNumber);
                e.Handled = true;
            }
        }
    }
}