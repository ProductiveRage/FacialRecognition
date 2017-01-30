# Facial Recognition Experiments

I've always been intrigued about how facial detection and recognition works, so I've decided to do a bit of reading and experimenting to see what I can come up with.

This is *not* going to be state of the art, it's not even likely to end up working very well - it's just something for me to play around with!

I'm hoping to go about it in three rough steps -

1. Try to guess where faces are in photos (probably using a simple algorithm that looks for areas of skin)
1. Categorise the regions identified above - is it a face or is it someone's hand
1. Perform some sort of facial matching to identify a face to a set of known faces

It's basically an excuse to tinker! And probably be an excuse to look into neural networks. And almost certainly provide me lots of opportunities to measure and optimise, which is always fun (must go fasterrrrrr!).

(Having found the article "[Machine Learning is Fun! Part 4: Modern Face Recognition with Deep Learning](https://medium.com/@ageitgey/machine-learning-is-fun-part-4-modern-face-recognition-with-deep-learning-c3cffc121d78#.v32w6yht7)", I suspect that I will change from the three step approach above at some point since the approach there seems much better! But I'm going to stick with Plan A first since I want to see how it pans out - I think it will much less efficient but I'll learn more doing it both ways :)

## Progress so far

If you were desperately excited to follow along and see how I'm doing - and run the code yourself - you need to clone this project, ensure that the project "Tester (Set me as Startup)" is configured to be the startup project and then you need to download some files from [http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/#Download](http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/#Download); the tar file containing images containing faces and a text file ("WebFaces_GroundThruth.txt") that contains points of interest within those image (such as where eyes, noses and mouths are within them). A "CaltechWebFaces" folder needs to be created where Tester.exe will be built (ie. bin/debug) and the tar file needs to have its content dumped in there, along with the text file).

Now you can run it!

It will extract 2000 training images from the Caltech WebFaces content (isolating the faces from the locations specified in the text file at three different zoom levels and then extracting non-face areas from the images for negative training images) which will be used to train a linear SVM.

The SVM will take data in the form of "histograms of gradients" taken from blocks across a source image (and normalised by comparing to adjacent blocks to try to minimise the effects of changes in contrast or lighting across an image). Given this data, the SVM will return a boolean result for whether it thinks that the image is a face or not.

In order to identify sections within an image that may be a face, it looks for regions that seem like they contain skin tones and then looks for enclosed skin tone regions that contain a few holes (for eye, nose, etc..). Any very small regions are ignored, as are any that are obviously completely the wrong aspect ratio. This will still generate false positives but the SVM should help filter those out\*.

Currently, only a single sample image is used to test this process - an image of Tiger Woods that I encountered in one of the articles I read about coding the skin tone filter.

To be honest, it's probably not the best example since the skin tone pass produces only one potential face region and zero false negatives - so the SVM filter doesn't have much to do! I will include more images that I've been using in testing at some point.

![Tiger Woods' face](https://raw.githubusercontent.com/ProductiveRage/FacialRecognition/master/TigerWoodsMatch.png)

When the program has run, it state how many potential face regions that the skin tone pass encountered and how many of those passed the SVM filter. A "Results" folder will be populated with all images extracted from the skin tone pass and will have an annotated output image for each input, which outlines the identified faces).

\* *Once the SVM has been generated, it's very fast to use it to test whether data describes a face or not, so it seems feasible that a "sliding window" could be moved across a source image to generate many possible-face-regions and the SVM could classify them - this would remove the need for the skin tone pass, which is relatively expensive (it's too slow to be used for real time face matching in video, for example). However, in early experiments I didn't get very good results with it. Chances are that I'll return to give it another shot in the future.*