using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    internal interface INotificationTemplate
    {
        string RenderEmail(string content);
        string RenderInApp(string content);
    }
}
