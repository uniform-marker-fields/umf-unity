UMF unity library and example
=======================

In case you end up using our source code in research, please add a reference to one of our relevant publications:

Binary UMF detection:

I. Szentandrasi, M. Zacharias, J. Havel, A. Herout, M. Dubska, and R. Kajan. Uniform Marker
Fields: Camera localization by orientable De Bruijn tori. In ISMAR 2012, 2012

Grayscale UMF detection:

Adam Herout, Istvan Szentandrasi, Michal Zacharias, Marketa Dubska, and Rudolf Kajan.
Five shades of grey for fast and reliable camera pose estimation. In Proceedings of CVPR,
pages 1384–1390. IEEE Computer Society, 2013

Unity plugin or Chromakeying UMF:

I. Szentandrasi, M. Dubska, M. Zacharias, and A. Herout. Poor man’s virtual camera:
Real-time simultaneous matting and camera pose estimation. IEEE Computer Graphics and
Applications, 2016

Usage
----------------------------------------

Place the plugins created from UMF detector into Assets/Plugins/x86 or Assets/Plugins/Android for android.

On windows also put the UMFDetector.dll next to the generated exe. Unity sometimes has trouble finding it.
Also make sure, that all libraries required by UMF detector are in the path.


Related Projects
----------------------------------------

* [UMF generator server](https://github.com/szist/umf-generator-server)
* [UMF generator client](https://github.com/szist/umf-generator-client)
* [UMF detector] (https://github.com/szist/umf-detector)
