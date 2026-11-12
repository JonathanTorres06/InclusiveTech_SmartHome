using UnityEngine;
using UnityEngine.Video;

public class Test : MonoBehaviour
{
    public GraphAgent miAgente;
    public string roomName;

    public bool isMoving;

    public VideoPlayer videoPlayer;

    void Start()
    {
        // Prepara el video en segundo plano
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        // El video ya está listo en memoria, ahora se reproduce sin tirones
        vp.Play();
    }

    /*void Update()
    {
        if (roomName != null && isMoving)
        {
            switch (roomName)
            {
                case "Entrada":
                    miAgente.GoToRoom("Entrada");
                    break;
                case "Pasillo":
                    miAgente.GoToRoom("Pasillo");
                    break;
                case "Sala":
                    miAgente.GoToRoom("Sala");
                    break;
                case "Comedor":
                    miAgente.GoToRoom("Comedor");
                    break;
                case "Cocina":
                    miAgente.GoToRoom("Cocina");
                    break;
                case "Baño":
                    miAgente.GoToRoom("Banio");
                    break;
                case "CuartoPrincipal":
                    miAgente.GoToRoom("CuartoPrincipal");
                    break;
                case "CuartoSecundario":
                    miAgente.GoToRoom("CuartoSecundario");
                    break;
            }

            isMoving = false;
        }
    }*/
}