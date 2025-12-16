using SSXLibrary.Models.Tricky;
using System.Diagnostics;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }

    [Fact]
    public void ExtractTest()
    {
        //This going to suck
        TrickyPS2MPF trickyPS2MPF = new TrickyPS2MPF();
        trickyPS2MPF.Load("");

        var Model = trickyPS2MPF.ConvertToBaseModel(0);

        string TestModel = Model.ValidFile();

        if(TestModel!="")
        {
            Debug.WriteLine(TestModel);
            throw new System.Exception(TestModel);
        }

    }
}