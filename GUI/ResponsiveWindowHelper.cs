using System;
using System.Windows;

namespace GUI
{
    public static class ResponsiveWindowHelper
    {
        public static void Ajustar(Window window, double anchoDiseno, double altoDiseno)
        {
            var area = SystemParameters.WorkArea;
            var anchoDisponible = Math.Max(720, area.Width * 0.96);
            var altoDisponible = Math.Max(520, area.Height * 0.94);

            window.Width = Math.Min(anchoDiseno, anchoDisponible);
            window.Height = Math.Min(altoDiseno, altoDisponible);
            window.MinWidth = Math.Min(window.MinWidth, Math.Max(720, anchoDisponible));
            window.MinHeight = Math.Min(window.MinHeight, Math.Max(520, altoDisponible));
            window.Left = area.Left + (area.Width - window.Width) / 2;
            window.Top = area.Top + (area.Height - window.Height) / 2;

            if (area.Width < anchoDiseno || area.Height < altoDiseno)
            {
                window.WindowState = WindowState.Maximized;
            }
        }
    }
}
