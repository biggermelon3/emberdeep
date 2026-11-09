using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepArtLightingTests
    {
        [UnityTest] public IEnumerator StandaloneLightingRestoresSharedPipelineOnCampReturn()
        {
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            var previous=QualitySettings.renderPipeline;
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var art=EmberdeepArtLibrary.Load();Assert.NotNull(art.Pipeline);
            Assert.AreSame(art.Pipeline,QualitySettings.renderPipeline);
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            Assert.Greater(game.Arena.GetComponentsInChildren<MeshFilter>().Length,10);
            game.ReturnToMenu();
            for(int i=0;i<120 && SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene;i++)yield return null;
            yield return null;
            Assert.AreSame(previous,QualitySettings.renderPipeline,"Returning from Emberdeep must restore the shared game's lighting profile");
        }
    }
}
