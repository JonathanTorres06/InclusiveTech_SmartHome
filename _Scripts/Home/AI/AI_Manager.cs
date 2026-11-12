using UnityEngine;
//using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine.InputSystem;
//using Oculus.Voice;
using System.Threading.Tasks;
using System.Collections.Generic;

public class AI_Manager : MonoBehaviour
{
    [Header("LLM Agent")]
    //public AIProviderBase providerAsset;
    //[SerializeField] private LlmAgent llmAgent;

    [Header("Speech To Text - STT")]
    //public AppVoiceExperience appVoice;

    [Header("User Question")]
    [TextArea(3, 70)]
    public string questionUser;

    //private IChatTask chatTask;
    private bool isProcessing = false;

    private void Start()
    {
        //chatTask = providerAsset as IChatTask;
    }

    private void OnEnable()
    {
        /*if (llmAgent == null || appVoice == null)
        {
            Debug.LogWarning("Error de asignacion de referencias");
            return;
        }

        appVoice.VoiceEvents.OnFullTranscription.AddListener(OnVoiceText);*/
    }

    private void OnDisable ()
    {
        /*if (llmAgent == null || appVoice == null)
        {
            Debug.LogWarning("Error de asignacion de referencias");
            return;
        }

        appVoice.VoiceEvents.OnFullTranscription.RemoveListener(OnVoiceText);*/
    }

    private void Update()
    {
        /*if (Keyboard.current.spaceKey.wasPressedThisFrame && !isProcessing)
        {
            appVoice.Activate();
            print("ESCUCHANDO...");
        }*/
    }

    /*public async void OnVoiceText(string text)
    {
        if (isProcessing)
        {
            return;
        }

        isProcessing = true;

        questionUser = text;
        Debug.Log("[TEXTO POR VOZ] " + questionUser);

        //Cortar microfono
        appVoice.Deactivate();

        await AskAI(questionUser);

        isProcessing = false;
    }*/

    // --- LLM GROQ ---

    /*private async Task AskAI(string userTxt)
    {
        ChatRequest request = new ChatRequest(userTxt);
        ChatResponse response = await chatTask.ChatAsync(request);

        Debug.Log("[RESPUESTA IA] " + response.text);
    }*/
}
