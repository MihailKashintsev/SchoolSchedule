using System.Windows;
using Kiosk.Models;

namespace Kiosk
{
    public partial class RoomInfoWindow : Window
    {
        public RoomInfoWindow(RoomInfo roomInfo)
        {
            InitializeComponent();
            DisplayRoomInfo(roomInfo);
        }

        private void DisplayRoomInfo(RoomInfo roomInfo)
        {
            TitleText.Text = $"Кабинет {roomInfo.RoomNumber}";
            RoomNumberText.Text = $"Номер: {roomInfo.RoomNumber}";
            RoomNameText.Text = roomInfo.Name;
            DescriptionText.Text = roomInfo.Description;
            ResponsibleText.Text = $"Ответственный: {roomInfo.Responsible}";
            TeacherText.Text = $"Преподаватель: {roomInfo.Teacher}";
            PhoneText.Text = $"Телефон: {roomInfo.Phone}";
            HoursText.Text = $"Часы работы: {roomInfo.Hours}";
            FloorText.Text = $"Этаж: {roomInfo.Floor}";
            PurposeText.Text = $"Назначение: {roomInfo.Purpose}";
            ScheduleText.Text = $"Расписание: {roomInfo.Schedule}";
            CurrentLessonText.Text = $"Текущее занятие: {roomInfo.CurrentLesson}";
            AdditionalInfoText.Text = roomInfo.AdditionalInfo;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}