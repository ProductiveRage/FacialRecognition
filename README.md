# Facial Recognition Experiments

I've always been intrigued about how facial detection and recognition works, so I've decided to do a bit of reading and experimenting to see what I can come up with.

This is *not* going to be state of the art, it's not even likely to end up working very well - it's just something for me to play around with!

I'm hoping to go about it in three rough steps -

1. Try to guess where faces are in photos (probably using a simple algorithm that looks for areas of skin)
1. Categorise the regions identified above - is it a face or is it someone's hand
1. Perform some sort of facial matching to identify a face to a set of known faces

It's basically an excuse to tinker! And probably be an excuse to look into neural networks. And almost certainly provide me lots of opportunities to measure and optimise, which is always fun (much go fasterrrrrr!).

(Having found the article "[Machine Learning is Fun! Part 4: Modern Face Recognition with Deep Learning](https://medium.com/@ageitgey/machine-learning-is-fun-part-4-modern-face-recognition-with-deep-learning-c3cffc121d78#.v32w6yht7)", I suspect that I will change from the three step approach above at some point since the approach there seems much better! But I'm going to stick with Plan A first since I want to see how it pans out - I think it will much less efficient but I'll learn more doing it both ways :)