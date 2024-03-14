using System.Collections.Generic;
using UnityEngine;

namespace JS.Architecture.SceneManagement
{
    [CreateAssetMenu(menuName = "Scene Management/Scene Collection")]
    public class SceneCollection : ScriptableObject
    {
        [field: SerializeField] public List<ScenePicker> Scenes { get; private set; }
    }
}