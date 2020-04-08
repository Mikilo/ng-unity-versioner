# NG Unity Versioner
Instantly check your code against any Unity version!


Window <b>NG Unity Versioner</b>

![Preview](https://forum.unity.com/attachments/upload_2019-12-21_19-13-36-png.533202/)


Here is an example of code in namespace beginning with "NGT" verified against 2020.1.0a16 :

![Code checked agaisnt 2020.1.0f16](https://forum.unity.com/attachments/upload_2019-12-21_18-57-57-png.533193/)


Verifying all versions :

![Code compatibilities summary](https://forum.unity.com/attachments/upload_2019-12-21_19-36-9-png.533208/)

## Steps A:
1. Open Window > NG Unity Versioner
2. Write your target namespace
3. Select the versions (in <i>Assembly Meta Versions</i> or <i>Unity Install Paths</i>)
4. Click on "Check Compatibilities"


If you would like to check against a Unity version not available but installed locally in your computer.
## Steps B:

1. Open Window > NG Unity Versioner
2. Click on <i>Installs</i> (on the right of <i>Unity Install Paths</i>)
3. Add path to a folder containing Unity installs (i.e. "C:/Program Files")
4. Do <b>Steps A</b>.


# How to install:
<dl>
<dt>Via git repository:</dt>
<dd>https://github.com/Mikilo/ng-unity-versioner</dd>

<dt>Via Package Manager (using "Add package from git URL..." ):</dt>

<dd>https://github.com/Mikilo/ng-unity-versioner.git</dd>

<dt>Via manifest.json:</dt>
<dd></dd>
</dl>

```
{
  "dependencies": {
    "com.mikilo.ng-unity-versioner": "https://github.com/Mikilo/ng-unity-versioner.git"
  }
}
```

# Requirements:
Unity 2017 LTS minimum
Framework .NET 4

Any feedback is welcome!
