using System.Collections.Generic;
using UnityEngine;


namespace VRSYS.Core.Chat.Odin
{
    [CreateAssetMenu(menuName = "Scriptable Objects/ChannelConfigurationInfo")]
    public class OdinRoomsConfigurationInfo : ScriptableObject
    {
        public List<OdinRoomConfiguration> roomConfigurations;
    }
}
