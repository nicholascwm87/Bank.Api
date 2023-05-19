By the assumption the token and everything is already exist.
I believe the system will have the Identity server ready for the authorization step.
I already wrote the code in the program.cs to have the authorization and i commented it out because this is just a test api is not really accessing any db and idp server.

For the logging I'm using the nLog for that and we should have a site that specifically for logging the information for error and the error handling filter created also will only log for some error when it hit.
As per the ErrorHandlingFilter.cs wrote cause like error 503 those when we do deployment it will be happen very frequent for so to not log repeatly i wrote the handling in between to handle that after 5 minute then log again.

As for the tracking like how many request and etc. I have been using Microsoft Azure to keep track all the request on it so I didn't have any tracking code in.

As for the Data because I don't know the db structure and also what will it inside the db.
So i just assumpt that the structure is well done will be like very good db structure and easily to access through foreign key and so on.
Of course for Microservice it will be good to separate call for transaction and customer. But due to limited thing i know about it i just assumpt is good..
So the code i wrote in is quite simple and straight forward.

Nunit testing didn't create because the system itself was too simple for this so I don't think it required nUnit testing for this cause i don't have any scenario base to test on.

