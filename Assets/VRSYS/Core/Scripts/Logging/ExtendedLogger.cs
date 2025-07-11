// VRSYS plugin of Virtual Reality and Visualization Group (Bauhaus-University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2023 Virtual Reality and Visualization Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Tony Jan Zoeppig, Sebastian Muehlhaus
//   Date:           2023
//-----------------------------------------------------------------

using UnityEngine;
using UnityEngine.Events;

namespace VRSYS.Core.Logging
{
    public class ExtendedLoggerLogInformation
    {
        public string FormattedMessage; // including color formatting for unity debug console
        public string ClearMessage; // log message without formatting 

        public ExtendedLoggerLogInformation(string formattedMessage, string clearMessage)
        {
            FormattedMessage = formattedMessage;
            ClearMessage = clearMessage;
        }
    }
    
    public class ExtendedLogger
    {
        #region Events

        public static UnityEvent<ExtendedLoggerLogInformation> OnInfoLog;
        public static UnityEvent<ExtendedLoggerLogInformation> OnWarningLog;
        public static UnityEvent<ExtendedLoggerLogInformation> OnErrorLog;

        #endregion
        
        #region Static Methods

        public static void LogInfo(string className, string message, Object context = null)
        {
            string formattedMessage = $"<color=white>[<color=green>Info</color>] [{className}] {message}</color>";
            Debug.Log(formattedMessage, context);

            string clearMessage = $"[{className}] {message}";
            
            OnInfoLog.Invoke(new ExtendedLoggerLogInformation(formattedMessage, clearMessage));
        }

        public static void LogWarning(string className, string message, Object context = null)
        {
            string formattedMessage = $"<color=white>[<color=yellow>Warning</color>] [{className}] {message}</color>";
            Debug.LogWarning(formattedMessage, context);
            
            string clearMessage = $"[{className}] {message}";
            
            OnWarningLog.Invoke(new ExtendedLoggerLogInformation(formattedMessage, clearMessage));
        }
        
        public static void LogError(string className, string message, Object context = null)
        {
            string formattedMessage = $"<color=white>[<color=red>Error</color>] [{className}] {message}</color>";
            Debug.LogError(formattedMessage, context);
            
            string clearMessage = $"[{className}] {message}";
            
            OnErrorLog.Invoke(new ExtendedLoggerLogInformation(formattedMessage, clearMessage));
        }

        #endregion
    }
}