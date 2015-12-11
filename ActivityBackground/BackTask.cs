using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ActivityBackground
{
    public sealed class BackTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var toastNotifier = ToastNotificationManager.CreateToastNotifier();
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
            var toastText = toastXml.GetElementsByTagName("text");
            (toastText[0] as XmlElement).InnerText = "Hello from background task";
            var toast = new ToastNotification(toastXml);
            toastNotifier.Show(toast);


            //ToastTemplateType toastTemplate = ToastTemplateType.ToastText02;
            //XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);
            //XmlNodeList textElements = toastXml.GetElementsByTagName("text");
            //textElements[0].AppendChild(toastXml.CreateTextNode("My first Task - Yeah"));
            //textElements[1].AppendChild(toastXml.CreateTextNode("I'm message from your background task!"));
            //ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(toastXml));
        }
    }
}
