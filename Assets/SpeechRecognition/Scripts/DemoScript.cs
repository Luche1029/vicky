using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrainCheck {


	public enum SpeechrecognitionOption 
	{
	  textToSpeech,
	  speechToTextWithSystemPopUp,
	  setUpPlugin,
	  speechToTextSilentMode,
	  unmuteSpeakers,
	  checkMicPermission,
	  requestMicPermission,
	  speechToTextInHidenModeWithBeepSound,
	  stopSpeechToTextConversion,
	  setVolumeLevel,
	  setMaxVolumeLevel,
	  speechToTextWithSystemPopUpAndParticularLanguage
	}

	public class DemoScript : MonoBehaviour
	{
		public SpeechrecognitionOption myOption;
		public string textToConvert;
		string gameObjectName = "UnityReceiveMessage";
		string statusMethodName = "CallbackMethod";
		int milliseconds = 60000;
		void OnMouseUp() {
	    	StartCoroutine(BtnAnimation());
	 	}

	 	private IEnumerator BtnAnimation()
	    {
	    	Vector3 originalScale = gameObject.transform.localScale;
	        gameObject.transform.localScale = 0.9f * gameObject.transform.localScale;
	        yield return new WaitForSeconds(0.2f);
	        gameObject.transform.localScale = originalScale;
	        ButtonAction();
	    }

	    private void ButtonAction() {
	    	BrainCheck.SpeechRecognitionBridgeAndroid.setUnityGameObjectNameAndMethodName(gameObjectName, statusMethodName);
			switch(myOption) 
			{
				case SpeechrecognitionOption.setUpPlugin:
			      BrainCheck.SpeechRecognitionBridgeAndroid.SetupPlugin();
			      break;
				case SpeechrecognitionOption.textToSpeech:
			      BrainCheck.SpeechRecognitionBridgeAndroid.textToSpeech(textToConvert, 0, 0.1f);  // 0 is for default locale. 0.1 is for speech rate.
			      break;
			    case SpeechrecognitionOption.setVolumeLevel:
			      BrainCheck.SpeechRecognitionBridgeAndroid.setVolumeForTextToSpeech(2);
			      break;
			    case SpeechrecognitionOption.setMaxVolumeLevel:
			      BrainCheck.SpeechRecognitionBridgeAndroid.setMaxVolumeForTextToSpeech();
			      break;
				case SpeechrecognitionOption.requestMicPermission:
			      BrainCheck.SpeechRecognitionBridgeAndroid.requestMicPermission();
			      break;
			    case SpeechrecognitionOption.checkMicPermission:
			      BrainCheck.SpeechRecognitionBridgeAndroid.checkMicPermission();
			      break;
			    case SpeechrecognitionOption.speechToTextWithSystemPopUp:
			      BrainCheck.SpeechRecognitionBridgeAndroid.speechToText("");
			      break;
			    case SpeechrecognitionOption.speechToTextWithSystemPopUpAndParticularLanguage:
			      BrainCheck.SpeechRecognitionBridgeAndroid.speechToText("hi");
			      break;
			    case SpeechrecognitionOption.speechToTextInHidenModeWithBeepSound:
			      BrainCheck.SpeechRecognitionBridgeAndroid.speechToTextInHidenModeWithBeepSound(milliseconds, "hi");
			      break;
			    case SpeechrecognitionOption.speechToTextSilentMode:
			      BrainCheck.SpeechRecognitionBridgeAndroid.speechToTextInSilentMode(milliseconds, "hi");
			      break;
			    case SpeechrecognitionOption.unmuteSpeakers:
			      BrainCheck.SpeechRecognitionBridgeAndroid.unmuteSpeakers();
			      break;
			    case SpeechrecognitionOption.stopSpeechToTextConversion:
			      BrainCheck.SpeechRecognitionBridgeAndroid.stopSpeechToTextConverter();
			      break;
			}
	    }
	}
}