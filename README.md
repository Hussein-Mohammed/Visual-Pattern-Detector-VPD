# Visual-Pattern Detector (VPD)

The VPD is an efficient and easy-to-use software tool for pattern detection. This tool can be used to recognise and allocate visual patterns (such as words, drawings and seals) automatically in digitised manuscripts. The recall-precision balance of detected patterns can be controlled visually, and the detected patterns can be saved as annotations on the original images or as cropped images depending on the needs of users.

![VPD](https://github.com/Hussein-Mohammed/Visual-Pattern-Detector-VPD/blob/master/vpd.png)

Please pay attention to the following limitations:
- The required computational resources depend on your data. Detecting several patterns concurrently can exhaust your computer memory, and searching in huge datasets is - limited by your storage capacity and your browser upload settings.
- This version provides best results of pattern detection when applied to datasets with relatively similar image qualities, such as resolution, degradation level, rotation, etc. an examples of such datasets are manuscripts that have been digitised under similar conditions and using similar equipements. Future releases will provide higher tolerance levels with respect to these aspects.
- This version has a limited capacity of benefiting from a large number of examples for the same pattern. Future releases will generate pattern models adaptively so that it gets better with every additional pattern example.
