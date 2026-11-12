using UnityEngine;
using UnityEngine.SceneManagement;

public class TestSceneLoaded : MonoBehaviour
{
    [SerializeField] private SistemaBiometrico sistemaBiometrico;

    public void NextTestScene() 
    {
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void NameUserRegister(int idDePrueba) 
    {
        sistemaBiometrico.OnRegistroFacialCompletado.Invoke(idDePrueba);
    }
}
