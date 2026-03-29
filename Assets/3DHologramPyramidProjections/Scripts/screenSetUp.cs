using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class screenSetUp : MonoBehaviour {


    //declaration of variables
    private static float ratioOfScreen;
    public GameObject TitleTop;
    public GameObject TitleBottom;
    public GameObject TitleLeft;
    public GameObject TitleRight;

    public GameObject MainCameraTop;
    public GameObject MainCameraBottom;
    public GameObject MainCameraLeft;
    public GameObject MainCameraRight;

    public GameObject InfoRUS;
    public GameObject InfoENG;
    public GameObject InfoPanel;

   
	
	// Update is called once per frame
	void Update () {

        ratioOfScreen = GetComponent<RectTransform>().sizeDelta.x / GetComponent<RectTransform>().sizeDelta.y;  //the length of the screen is divided by width

        // iPad(all models) = 1.33 // iPhone4,iPhone = 1.5 // android 1.5 // tablets //by default
        if (ratioOfScreen <= 1.5f)
        {
            //print("iPhone4, iPhone / iPad / tablets / ratioOfScreen = " + ratioOfScreen);

            //by default
            TitleTop.transform.localScale = new Vector3(0.3350461f, 0.3350461f, 0.3350461f);
            TitleBottom.transform.localScale = new Vector3(0.3350461f, 0.3350461f, 0.3350461f);
            TitleLeft.transform.localScale = new Vector3(0.3350461f, 0.3350461f, 0.3350461f);
            TitleRight.transform.localScale = new Vector3(0.3350461f, 0.3350461f, 0.3350461f);

            TitleLeft.transform.localPosition = new Vector3(235f, -3f, 0f);
            TitleRight.transform.localPosition = new Vector3(-226.22f, -2.0f, 0f);

            MainCameraLeft.transform.localPosition = new Vector3(9.6f, 2.33f, 0.04f);
            MainCameraRight.transform.localPosition = new Vector3(-132.22f, 0.69f, -0.28f);

        }
        //iPhone5,iPhone6,iPhone6Plus,iPhone6s,iPhone6s_plus,iPhone7,iPhone7_plus,iPhone8,iPhone8_plus,iPhoneSE = 1.77  //android 1.59 / 1.6 / 1.66 / 1.7 / 1.77
        else if ((ratioOfScreen > 1.5) && (ratioOfScreen < 2.0)) 
        {
            //print("all iPhone less iPhoneX / all Android-phones / ratioOfScreen = " + ratioOfScreen);

            TitleTop.transform.localScale = new Vector3(0.241808f, 0.241808f, 0.241808f);
            TitleBottom.transform.localScale = new Vector3(0.241808f, 0.241808f, 0.241808f);
            TitleLeft.transform.localScale = new Vector3(0.241808f, 0.241808f, 0.241808f);
            TitleRight.transform.localScale = new Vector3(0.241808f, 0.241808f, 0.241808f);

            TitleLeft.transform.localPosition = new Vector3(156.3f, -3f, 0f);
            TitleRight.transform.localPosition = new Vector3(-156.5f, -2f, 0f);

            MainCameraLeft.transform.localPosition = new Vector3(9.6f, 4.23f, 0.04f);
            MainCameraRight.transform.localPosition = new Vector3(-132.22f, 2.96f, -0.28f);


        }
        //iPhoneX = 2.16  //scale title =0.183307  //left title posX = 139.9 // right title posX = -132.4  //camera left y=5.54 //camera right y=4.32
        else if (ratioOfScreen >= 2.0f)
        {
            //print("iPhoneX and high / ratioOfScreen = " + ratioOfScreen);

            TitleTop.transform.localScale = new Vector3(0.183307f, 0.183307f, 0.183307f);
            TitleBottom.transform.localScale = new Vector3(0.183307f, 0.183307f, 0.183307f);
            TitleLeft.transform.localScale = new Vector3(0.183307f, 0.183307f, 0.183307f);
            TitleRight.transform.localScale = new Vector3(0.183307f, 0.183307f, 0.183307f);

            TitleLeft.transform.localPosition = new Vector3(139f, -3f, 0f);
            TitleRight.transform.localPosition = new Vector3(-132f, -2f, 0f);

            MainCameraLeft.transform.localPosition = new Vector3(9.6f, 5.98f, 0.04f);
            MainCameraRight.transform.localPosition = new Vector3(-132.22f, 4.32f, -0.28f);


        }



    }



    public void ShowInfoRUS()
    {
        InfoPanel.SetActive(true);
        InfoRUS.SetActive(true);
        InfoENG.SetActive(false);
    }


    public void ShowInfoENG()
    {
        InfoPanel.SetActive(true);
        InfoENG.SetActive(true);
        InfoRUS.SetActive(false);
    }


    public void CloseInfoENG_RUS()
    {
        InfoPanel.SetActive(false);
        InfoENG.SetActive(false);
        InfoRUS.SetActive(false);
    }


    public void YouTube_3DHologramPyramidProjections()
    {
        //print ("URL YouTube_3DHologramPyramidProjections");
        Application.OpenURL("https://youtu.be/A7ebyf8lbew");

    }


    public void EnvoySoftSite()
    {
        //print ("URL YouTube_3DHologramPyramidProjections");
        //Application.OpenURL("http://www.envoysoft.ru");
        Application.OpenURL("https://www.youtube.com/watch?v=ScKkDThn_nE");
    

    }


    //Application.OpenURL("https://itunes.apple.com/app/wrapped-in-mystery/id1457323362"); //ENG
    //Application.OpenURL("https://itunes.apple.com/ru/app/wrapped-in-mystery/id1457323362"); //RUS
    //Application.OpenURL("https://play.google.com/store/apps/details?id=com.envoysoft.wrappedInMysteryA");



}
