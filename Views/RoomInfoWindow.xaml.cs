using System.Windows;

namespace Kiosk.Views
{
    public partial class RoomInfoWindow : Window
    {
        public RoomInfoWindow(Models.RoomInfo room)
        {
            InitializeComponent();

            // Прямое присвоение значений
            RoomNumberText.Text = room.RoomNumber;
            PurposeText.Text = room.Purpose;
            TeacherText.Text = room.Teacher;
            LessonText.Text = room.CurrentLesson;
            ScheduleText.Text = room.Schedule;
            AdditionalInfoText.Text = room.AdditionalInfo;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}