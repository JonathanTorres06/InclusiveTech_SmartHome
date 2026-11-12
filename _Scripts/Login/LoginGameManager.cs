using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class LoginGameManager : MonoBehaviour
{
    [Header("Configuraciones")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Secuencia de Login")]
    private LoginFlowManager loginFlowManager;

    [Header("Video de Fondo")]
    [SerializeField] VideoPlayer videoPlayer;

    void Start()
    {
        // Prepara el video en segundo plano
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();

        StartWelcomeSecuence();
    }

    async void StartWelcomeSecuence() 
    {
        loginFlowManager = LoginFlowManager.Instance;

        if (loginFlowManager == null)
        {
            Debug.LogError("[LoginGameManager] No encontré LoginFlowManager. ¿Está el objeto CORE en BootScene?");
            return;
        }

        try
        {
            await loginFlowManager.PlayWelcomeSequenceAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginGameManager] Error en la secuencia de bienvenida: {ex.Message}");
        }

    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        // El video ya está listo en memoria, ahora para tratar de que se reproduzca sin tirones
        vp.Play();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) { videoPlayer.prepareCompleted -= OnVideoPrepared; }
    }
}
