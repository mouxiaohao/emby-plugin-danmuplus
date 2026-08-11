#!/usr/bin/env python3
"""Verify the live Dandan XML mapping used by the r3 acceptance run."""

import glob
import hashlib
import os
import sys
import xml.etree.ElementTree as ET


BASELINE = {
    1: [
        "96ba7891d31c605e5c164f2a574bd1fe0f2993300ecd31992429a3485b48c6d6",
        "0668531f2cea1931430095c35c11f373d90d4465ef59c1f18fd28f37abe7a094",
        "af751c43523443a3297e9eb5fb3cfc5c4170b619255f695bc6e4277627c42b94",
        "c38b4b25c76a9f873438ece45ebe082c315548a181cb8b06bcd27fcab0b09dda",
        "fc48c3cc97054dece575503eedba78fb9ee2e2ae0d84d5150c0dc57fe4eeff6b",
        "89b33883f0b470ef963a35636921854844be0dee2a793605085e969e0cff46bc",
        "6ab4880a51bc5b8ed00c755ad2cd616eb1c337693229ace14172f02975210ac1",
        "984a9ea7a2946207fd5a462e44e1f46317c91111e93edb6679686ce40ed5f0a0",
        "25ec884e93409859a97ef2efc715d52963a816080bf8ba3f9c595d6dfc307706",
        "25767cc1137e8a556139961162d82b2c56586d7aa53994a37b69cf131a271893",
        "993f34a28c56adf8b8712ea49ab827cb75fc1a8b13b0821fe712cb7483f3eba3",
        "42ec2b809705933c0099728e9aa55158c140889241a5c7deca2c20a11d980501",
        "fd793479d30cd65f18b11b37e7d307fb2118cdeb863a4fd8962a71d33a073018",
        "56f3d23793d64403ab1e90efe8149f0bb6b20be914f3fe2848421d2eb288fc5f",
    ],
    4: [
        "4e6e400f883450e54a93753ca7340c0958a3f15d2b90d211bce97f6b32b9c4cf",
        "2b9cfec5ca2fb270842b3f12004525b81bdd75db159f4f9c8afb4527cc9b92f7",
        "d7c2a053fa76f16700418ffd0748eade7de404d7bf6607f4b32e372fd29cf677",
        "6ee6d03f300bc2462adc247ac18cca7e0a4eddacc6d61116a023acf35f98d939",
        "ac9831abba14ea7a7a38765c0231e5163309db0d479146460f6c16834a971b7e",
        "8dd3db220da0881c23a6b617c1d0d590ca697cacea16903cb46dd6b365c83026",
        "c117cb23aaba4005ed7b0a829ff0520ba33b4cf8aed75c2d8eb498e2460b8100",
        "8ee4a48cb5752edcd1597cbbfce502228d156c2b64168bb82f46f276b8e6fc5d",
        "c9cfb7135882335eafb2dd926446e3da96b5b849de02b1f864fa91d0be416338",
        "5ef85746b2d3b1a73c9139f67f0374c26988a714d8b804a274bf8475a6f16606",
        "4362297c1f37bf5ceec827be4c0ed1f28b6cae9705e3b74c41ca4d302a9ae9d1",
        "c425b1f752c89dd946125927e907aad2d9e8f4e50266a0d0be27a93ddd4d51a4",
        "bf7d7d4f9533003335ee39bb23ac5754d89a2922500906abb0167b0d47843d45",
        "8ff8bf46e595001c53c590b28319c4d584ff1b432e31528cb0710a887754f9e8",
        "a457989c6f6c14ee47168bb90232dad1faa874c4fe01b19f543f3e05cb1fed2d",
        "6b6e408cadeb0e69ad972565aa4de2225e2bbc33cb5622cd8fc482428a811caf",
    ],
}


def files_for(root: str, season: int):
    pattern = os.path.join(root, f"Season{season}", "*_DandanID.xml")
    return sorted(glob.glob(pattern))


def digest(path: str) -> str:
    with open(path, "rb") as stream:
        return hashlib.sha256(stream.read()).hexdigest()


def main() -> int:
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify_dandan_xml.py SERIES_DIRECTORY")
    root = sys.argv[1]

    for season, expected in BASELINE.items():
        actual_files = files_for(root, season)
        actual = [digest(path) for path in actual_files]
        if actual != expected:
            raise RuntimeError(f"Season {season} baseline hashes changed")
        print(f"Season{season}: baseline-unchanged files={len(actual)}")

    for season, anime_id, count in (
        (1, 14727, 14),
        (2, 15293, 12),
        (3, 15634, 10),
        (4, 18302, 16),
    ):
        actual_files = files_for(root, season)
        if len(actual_files) != count:
            raise RuntimeError(f"Season {season} expected {count} Dandan XML files, got {len(actual_files)}")
        chatids = []
        comments = []
        for index, path in enumerate(actual_files, 1):
            root_element = ET.parse(path).getroot()
            chatid = root_element.findtext("chatid")
            comment_count = len(root_element.findall(".//d"))
            expected_chatid = str(anime_id * 10000 + index)
            if chatid != expected_chatid or comment_count <= 0:
                raise RuntimeError(
                    f"Season {season} episode {index}: chatid={chatid}, comments={comment_count}"
                )
            chatids.append(chatid)
            comments.append(comment_count)
        print(
            f"Season{season}: files={count} chatids={chatids[0]}..{chatids[-1]} "
            f"comments=min:{min(comments)},total:{sum(comments)}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
