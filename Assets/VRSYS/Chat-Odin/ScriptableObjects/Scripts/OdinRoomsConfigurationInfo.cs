using System.Collections.Generic;
using UnityEngine;


namespace VRSYS.Core.Chat.Odin
{
    [CreateAssetMenu(menuName = "VRSYS/Chat-Odin/Scriptable Objects/ChannelConfigurationInfo")]
    public class OdinRoomsConfigurationInfo : ScriptableObject
    {
        public List<OdinRoomConfiguration> roomConfigurations;
    }
}
