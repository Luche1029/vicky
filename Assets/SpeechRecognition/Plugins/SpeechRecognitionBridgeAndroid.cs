using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;


namespace BrainCheck {

	public class SpeechRecognitionBridgeAndroid {

		static AndroidJavaClass _class;
		static AndroidJavaObject instance { get { return _class.GetStatic<AndroidJavaObject>("instance"); } }

		public	 static void SetupPlugin () {
			if (_class == null) {
				_class = new AndroidJavaClass ("mayankgupta.com.textToSpeech.TextToSpeechPlugin");
				_class.CallStatic ("_initiateFragment");
			}
		}

		/* This method is used for converting text to speech. It accepts following params:
        	1. text - The text which needs to be converted.
        	2. locale -- Language Code // These are available in ReadMe file
        	3. speechRate  - The speed with which the plugin with translate the text to speech
       	   Once conversion starts it send callback "TextToSpeechConversionStarted" and once conversion complete it sends "TextToSpeechConversionCompleted"
     	*/
		public static void textToSpeech(string text, int locale, float speechRate = -1){
			SetupPlugin ();
		   	instance.Call("convertTextToSpeech", text, locale, speechRate);
		}

		//To set volume level of the speech use following code
		public static void setVolumeForTextToSpeech(int volume){
			SetupPlugin ();
		   	instance.Call("setVolumeForTextToSpeech", volume);
		}

		//To set volume level maximum of the speech use following code
		public static void setMaxVolumeForTextToSpeech(){
			SetupPlugin ();
		   	instance.Call("setMaxVolumeForTextToSpeech");
		}

		//To request mic permission use following code
		public static void requestMicPermission(){
			SetupPlugin ();
		   	instance.Call("requestMicrophonePermission");
		}

		//To check mic permission use following code
		public static void checkMicPermission(){
			SetupPlugin ();
		   	instance.Call("checkMicrophonePermission");
		}

		//To convert speech to text use following code. This method will show a pop up. To check the locale codes refer https://en.wikipedia.org/wiki/IETF_language_tag
		public static void speechToText(string locale){
			SetupPlugin ();
		   	instance.Call("startSpeechToTextConversion", locale);
		}

		//To convert speech to text without System pop up but with beep sound use following code. To check the locale codes refer https://en.wikipedia.org/wiki/IETF_language_tag
		public static void speechToTextInHidenModeWithBeepSound(int milliseconds= -1, string locale = ""){
			SetupPlugin ();
		   	instance.Call("startHiddenSpeechToTextConversion", milliseconds, locale);
		}

		//To convert speech to text without System pop up and without sound use following code. To check the locale codes refer https://en.wikipedia.org/wiki/IETF_language_tag
		public static void speechToTextInSilentMode(int milliseconds = -1, string locale = ""){
			SetupPlugin ();
		   	instance.Call("startSilentSpeechToTextConversion", milliseconds, locale);
		}

		public static void unmuteSpeakers(){
			SetupPlugin ();
		   	instance.Call("unMuteAudioManager");
		}

		public static void stopSpeechToTextConverter(){
			SetupPlugin ();
		   	instance.Call("stopSilentSpeechToTextConversion");
		}

		public static void setUnityGameObjectNameAndMethodName(string gameObjectName, string statusMethodName){
			SetupPlugin ();
			Debug.Log("callback");
		   	instance.Call("_setUnityGameObjectNameAndMethodName", gameObjectName, statusMethodName);
		}

	}
}